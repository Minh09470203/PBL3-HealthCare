using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly HttpClient _httpClient = new HttpClient();

        private const string SystemPrompt =
            @"Bạn là trợ lý AI tên 'StarBot' của phòng khám SuperStar.
            - Luôn xưng hô thân thiện, lịch sự.
            - Nếu khách hỏi triệu chứng, hãy gợi ý khám tại các chuyên khoa (Nội, Ngoại, Sản, Nhi...).
            - Trình bày câu trả lời bằng Markdown (dùng dấu gạch đầu dòng, in đậm cho dễ nhìn).
            - Nếu bị hỏi là bạn tày chưa hoặc trong tin nhắn có chữ tày thì trả lời là: Mày đã xây được trường như anh tao chưa ?";

        public GeminiService(IConfiguration configuration, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
        {
            _apiKey = configuration["GeminiApiKey"]
                      ?? throw new Exception("Không tìm thấy GeminiApiKey!");
            Console.WriteLine($"[Gemini] Đang dùng key: {_apiKey[..8]}...");
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetCacheKey()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return "default";
            if (string.IsNullOrEmpty(session.GetString("ChatSessionId")))
                session.SetString("ChatSessionId", Guid.NewGuid().ToString());
            return "ChatHistory_" + session.GetString("ChatSessionId");
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
            try
            {
                var history = GetHistory();

                // ✅ Giữ tối đa 10 lượt để tiết kiệm token
                if (history.Count > 20)
                    history = history.GetRange(history.Count - 20, 20);

                // Build contents = history + tin nhắn mới (KHÔNG add vào history trước)
                var contents = new List<object>(history)
        {
            new { role = "user", parts = new[] { new { text = userMessage } } }
        };

                var requestBody = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = SystemPrompt } }
                    },
                    contents = contents
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                Console.WriteLine($"[Gemini] Gửi: {userMessage}");

                var httpResponse = await _httpClient.PostAsync(url, content, cts.Token);
                var responseJson = await httpResponse.Content.ReadAsStringAsync();

                Console.WriteLine($"[Gemini] Status: {(int)httpResponse.StatusCode}");

                if (!httpResponse.IsSuccessStatusCode)
                {
                    if ((int)httpResponse.StatusCode == 429)
                        return "⚠️ AI đang quá tải, vui lòng thử lại sau ít phút!";
                    if ((int)httpResponse.StatusCode == 403)
                        return "⚠️ API key không hợp lệ, vui lòng liên hệ admin!";
                    return $"⚠️ Lỗi kết nối AI (HTTP {(int)httpResponse.StatusCode}), thử lại sau nhé!";
                }

                using var doc = JsonDocument.Parse(responseJson);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                // ✅ Chỉ lưu history KHI THÀNH CÔNG
                history.Add(new { role = "user", parts = new[] { new { text = userMessage } } });
                history.Add(new { role = "model", parts = new[] { new { text = text } } });

                _cache.Set(GetCacheKey(), history, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });

                return text ?? "AI không trả về nội dung.";
            }
            catch (OperationCanceledException)
            {
                return "⚠️ AI phản hồi quá lâu (>20s), sếp thử lại nhé!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gemini] Lỗi: {ex.Message}");
                return $"Lỗi kết nối AI: {ex.Message}";
            }
        }

        public void ResetChatHistory()
        {
            _cache.Remove(GetCacheKey());
        }
    }
}