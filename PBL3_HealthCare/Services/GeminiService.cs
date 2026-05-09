using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models; // NHỚ USING THÊM MODEL CỦA SẾP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Services
{
    // Đưa 2 Model vào đây để dùng chung
    public class ChatSession
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string LastMsg { get; set; }
        public DateTime Time { get; set; }
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Time { get; set; }
    }

    public class GeminiService
    {
        private readonly List<string> _apiKeys;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string BaseSystemPrompt =
            @"Bạn là trợ lý AI tên 'StarBot' của phòng khám SuperStar.
            - Luôn xưng hô thân thiện, lịch sự.
            - Nếu khách hỏi triệu chứng, hãy gợi ý khám tại các chuyên khoa phù hợp.
            - Nếu bị hỏi 'Ê thằng tày' hoặc tin nhắn có chữ 'tày' thì trả lời gắt: 'Mày đã xây được trường như anh tao chưa?'.";

        public GeminiService(IConfiguration configuration, IMemoryCache cache, ApplicationDbContext context)
        {
            _apiKeys = configuration.GetSection("GeminiApiSettings:ApiKeys").Get<List<string>>()
                       ?? new List<string> { configuration["GeminiApiKey"] };
            _cache = cache;
            _context = context;
        }

        // ── HÀM CHÍNH ĐÃ NHẬN THÊM sessionId VÀ userId TỪ CONTROLLER ──
        public async Task<string> GetMedicalAdviceAsync(string userMessage, string userId, string sessionId)
        {
            var doctors = await _context.Doctors.Include(d => d.User).Include(d => d.Specialty).Take(5).ToListAsync();
            string doctorList = string.Join("\n", doctors.Select(d => $"- BS: {d.User?.FullName} (ID: {d.Id}) - Khoa: {d.Specialty?.Name} - Ảnh: {d.Image ?? "null"}"));

            string finalSystemPrompt = $@"{BaseSystemPrompt}
                DANH SÁCH BÁC SĨ:
                {doctorList}
                LUẬT (BẮT BUỘC): 
                - Top 5 bác sĩ sẽ được liệt kê ở trên. Gợi ý bác sĩ dùng mã: [BOOKING:ID|Tên|Ảnh]. 
                - Copy đầy đủ 100% từng ký tự của tên ảnh. Nếu không có ảnh ghi 'null'.";

            // 1. LẤY TRÍ NHỚ CHO AI (Theo sessionId chung)
            string aiHistoryKey = $"AI_History_{sessionId}";
            var history = _cache.GetOrCreate(aiHistoryKey, entry => {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new List<object>();
            });

            if (history.Count > 6) history = history.GetRange(history.Count - 6, 6);
            var contents = new List<object>(history) { new { role = "user", parts = new[] { new { text = userMessage } } } };

            var requestBody = new { system_instruction = new { parts = new[] { new { text = finalSystemPrompt } } }, contents = contents };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            foreach (var key in _apiKeys)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={key.Trim()}";
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                    var httpResponse = await _httpClient.PostAsync(url, content, cts.Token);
                    var responseJson = await httpResponse.Content.ReadAsStringAsync();

                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)httpResponse.StatusCode >= 500) continue;

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        // Đọc thẳng câu chửi gốc của Google trả về
                        var errorDetail = await httpResponse.Content.ReadAsStringAsync();
                        return $"⚠️ LỖI THẬT TỪ GOOGLE ({httpResponse.StatusCode}): {errorDetail}";
                    }

                    using var doc = JsonDocument.Parse(responseJson);
                    var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                    // 2. CHỈ LƯU LỊCH SỬ KHI GỌI API THÀNH CÔNG (Tránh lưu câu báo lỗi)
                    history.Add(new { role = "user", parts = new[] { new { text = userMessage } } });
                    history.Add(new { role = "model", parts = new[] { new { text = text } } });
                    _cache.Set(aiHistoryKey, history, TimeSpan.FromMinutes(30));

                    SaveUIHistoryToCache(userMessage, text, userId, sessionId); // Hàm phụ lưu cho Giao diện

                    return text;
                }
                catch (Exception ex)
                {
                    return $"⚠️ Lỗi trong code C#: {ex.Message}";
                }
            }
            return "⚠️ Đã thử hết các Key nhưng không thành công.";
        }

        private void SaveUIHistoryToCache(string userMessage, string aiResponse, string userId, string sessionId)
        {
            var msgKey = $"msgs_{userId}_{sessionId}";
            var msgs = _cache.Get<List<ChatMessage>>(msgKey) ?? new List<ChatMessage>();

            if (msgs.Count == 0) // Lượt đầu → lưu thông tin session
            {
                var sessKey = $"sessions_{userId}";
                var sessions = _cache.Get<List<ChatSession>>(sessKey) ?? new List<ChatSession>();

                sessions.Insert(0, new ChatSession
                {
                    Id = sessionId,
                    Title = userMessage.Length > 40 ? userMessage[..40] + "..." : userMessage,
                    LastMsg = aiResponse.Length > 50 ? aiResponse[..50] + "..." : aiResponse,
                    Time = DateTime.Now
                });

                if (sessions.Count > 10) sessions = sessions.Take(10).ToList();
                _cache.Set(sessKey, sessions, TimeSpan.FromDays(7));
            }

            msgs.Add(new ChatMessage { Role = "user", Content = userMessage, Time = DateTime.Now });
            msgs.Add(new ChatMessage { Role = "ai", Content = aiResponse, Time = DateTime.Now });
            _cache.Set(msgKey, msgs, TimeSpan.FromDays(7));
        }

        // Xóa sạch cả não AI lẫn tin nhắn Giao diện
        public void ResetChatHistory(string userId, string sessionId)
        {
            _cache.Remove($"AI_History_{sessionId}");
            _cache.Remove($"msgs_{userId}_{sessionId}");
        }
    }
}