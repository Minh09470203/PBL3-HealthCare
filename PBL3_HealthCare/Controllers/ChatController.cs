using Microsoft.AspNetCore.Mvc;
using PBL3_HealthCare.Services;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    public class ChatController : Controller
    {
        private readonly GeminiService _geminiService;

        public ChatController(GeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        [HttpPost]
        public async Task<IActionResult> AskAI(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Json(new { success = false, data = "Tin nhắn không được để trống sếp ơi!" });

            // Gọi Service để hỏi Gemini
            var answer = await _geminiService.GetMedicalAdviceAsync(message);

            return Json(new { success = true, data = answer });
        }
    }
}