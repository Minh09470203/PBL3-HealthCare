using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Services
{
    public class GeminiService
    {
        private readonly string _apiKey = "AIzaSyDQ8eTiLrIdS623QoLr5J5OyL2RTc1Bjmo";

        private List<ContentResponse> _chatHistory = new List<ContentResponse>();

        private const string SystemPrompt =
            @"Bạn là trợ lý AI tên 'StarBot' của phòng khám SuperStar.
            - Luôn xưng hô thân thiện, lịch sự.
            - Nếu khách hỏi triệu chứng, hãy gợi ý khám tại các chuyên khoa (Nội, Ngoại, Sản, Nhi...).
            - Trình bày câu trả lời bằng Markdown (dùng dấu gạch đầu dòng, in đậm cho dễ nhìn).
            - Nếu bị hỏi là bạn tày chưa hoặc trong tin nhắn có chữ tày thì trả lời là: Mày đã xây được trường như anh tao chưa ?";

        public async Task<string> GetMedicalAdviceAsync(string userMessage)
        {
            try
            {
                var googleAI = new GoogleAI(_apiKey);

                // ✅ FIX: Wrap systemInstruction đúng kiểu Content
                var systemInstruction = new Content
                {
                    Parts = new List<IPart>
                    {
                     new Part { Text = SystemPrompt }
                    },
                    Role = "system"
                };

                var model = googleAI.GenerativeModel(
                    "gemini-2.5-flash",
                    systemInstruction: systemInstruction
                );

                var chat = model.StartChat(_chatHistory);

                var response = await chat.SendMessage(userMessage);

                _chatHistory = chat.History ?? new List<ContentResponse>();

                return response?.Text ?? "AI không trả về nội dung, sếp kiểm tra lại nhé.";
            }
            catch (Exception ex)
            {
                return $"Lỗi kết nối AI: {ex.Message}";
            }
        }

        public void ResetChatHistory()
        {
            _chatHistory = new List<ContentResponse>();
        }
    }
}