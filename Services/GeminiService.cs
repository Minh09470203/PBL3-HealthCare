using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PBL3_HealthCare.Data;
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
    public class GeminiService
    {
        private readonly List<string> _apiKeys;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string BaseSystemPrompt =
            @"Bạn là trợ lý AI tên 'StarBot' của phòng khám SuperStar.
            - Luôn xưng hô thân thiện, lịch sự.
            - Nếu khách hỏi triệu chứng, hãy gợi ý khám tại các chuyên khoa phù hợp.
            - Nếu bị hỏi 'Ê thằng tày' hoặc tin nhắn có chữ 'tày' thì trả lời gắt: 'Mày đã xây được trường như anh tao chưa?'.";

        public GeminiService(IConfiguration configuration, IMemoryCache cache, IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
        {
            // Lấy danh sách Key từ appsettings.json
            _apiKeys = configuration.GetSection("GeminiApiSettings:ApiKeys").Get<List<string>>()
                       ?? new List<string> { configuration["GeminiApiKey"] };

            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        private string GetCacheKey()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var sessionId = session?.GetString("ChatSessionId") ?? "default";
            return "ChatHistory_" + sessionId;
        }

        private List<object> GetHistory()
        {
            return _cache.GetOrCreate(GetCacheKey(), entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                return new List<object>();
            });
        }

        public async Task<string> GetMedicalAdviceAsync(string userMessage)
        {
            // 1. MÓC DATA & TỐI ƯU TOKEN
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .Take(5) 
                .ToListAsync();

            string doctorList = string.Join("\n", doctors.Select(d =>
                $"- BS: {d.User?.FullName} (ID: {d.Id}) - Khoa: {d.Specialty?.Name} - Ảnh: {d.Image ?? "null"}"));

            string finalSystemPrompt = $@"{BaseSystemPrompt}
                DANH SÁCH BÁC SĨ:
                {doctorList}
                
                LUẬT (BẮT BUỘC): 
                - Top 5 bác sĩ sẽ được liệt kê ở trên, nếu khách hỏi về chuyên khoa nào thì gợi ý bác sĩ thuộc chuyên khoa đó.
                - Gợi ý bác sĩ dùng mã: [BOOKING:ID|Tên|Ảnh]. 
                - 🚨 QUAN TRỌNG: Tên ảnh là một chuỗi mã hóa. Bạn TUYỆT ĐỐI KHÔNG ĐƯỢC VIẾT TẮT (không dùng dấu ...). BẮT BUỘC phải copy đầy đủ 100% từng ký tự của tên ảnh từ danh sách trên.
                - Ví dụ chuẩn: [BOOKING:2|BS. Phúc|65732b1b-ac39-4b01-975c-6cc388b6500a_maxresdefault.jpg]. Nếu không có ảnh ghi 'null'.";

            var history = GetHistory();
            if (history.Count > 6) history = history.GetRange(history.Count - 6, 6);

            var contents = new List<object>(history) { new { role = "user", parts = new[] { new { text = userMessage } } } };

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = finalSystemPrompt } } },
                contents = contents
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 2. XOAY TUA KEY (LOAD BALANCING)
            foreach (var key in _apiKeys)
            {
                try
                {
                    // Lọc những key trống do lỗi cấu hình file appsettings
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={key.Trim()}";
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                    var httpResponse = await _httpClient.PostAsync(url, content, cts.Token);
                    var responseJson = await httpResponse.Content.ReadAsStringAsync(); // Đọc kết quả ngay lập tức

                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                        httpResponse.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                        (int)httpResponse.StatusCode >= 500)
                    {
                        Console.WriteLine($"[Gemini] Key {key[..5]}... bị từ chối do tải nặng ({httpResponse.StatusCode}), chuyển sang key dự phòng.");
                        continue; // Bỏ qua key này, vòng lặp sẽ chạy thử key tiếp theo
                    }

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        // CHỖ NÀY QUAN TRỌNG: Nếu Google báo lỗi (400, 403, 404...), in thẳng ra màn hình chat cho sếp xem!
                        Console.WriteLine($"[LỖI GEMINI] Status: {httpResponse.StatusCode}. Chi tiết: {responseJson}");
                        return $"⚠️ Lỗi Google API ({httpResponse.StatusCode}): Hãy thử lại sau giây lát.";
                    }

                    // Nếu thành công 200 OK
                    using var doc = JsonDocument.Parse(responseJson);
                    var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                    history.Add(new { role = "user", parts = new[] { new { text = userMessage } } });
                    history.Add(new { role = "model", parts = new[] { new { text = text } } });
                    _cache.Set(GetCacheKey(), history, TimeSpan.FromMinutes(30));

                    return text;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] Code C# bị lỗi: {ex.Message}");
                    return $"⚠️ Lỗi trong code C#: {ex.Message}";
                }
            }
            return "⚠️ Đã thử hết các Key nhưng không thành công. Sếp xem tin nhắn lỗi ở trên nhé!";
        }
        public void ResetChatHistory()
        {
            _cache.Remove(GetCacheKey());
        }

    }
}