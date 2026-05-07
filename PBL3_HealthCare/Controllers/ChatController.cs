using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    public class ChatController : Controller
    {
        private readonly GeminiService _geminiService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;

        public ChatController(GeminiService geminiService, UserManager<ApplicationUser> userManager, IMemoryCache cache)
        {
            _geminiService = geminiService;
            _userManager = userManager;
            _cache = cache;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AskAI(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return Json(new { success = false, data = "Tin nhắn không được để trống!" });

            var userId = _userManager.GetUserId(User);

            // Xử lý cấp Cookie SessionId nếu chưa có
            var sessionId = Request.Cookies["chatSessionId"];
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N")[..8];
                Response.Cookies.Append("chatSessionId", sessionId, new CookieOptions { MaxAge = TimeSpan.FromDays(30) });
            }

            // Giao phó toàn bộ việc gọi AI và lưu lịch sử cho Service
            var response = await _geminiService.GetMedicalAdviceAsync(message, userId, sessionId);

            return Json(new { success = true, data = response });
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetSessions()
        {
            var userId = _userManager.GetUserId(User);
            var sessions = _cache.Get<List<ChatSession>>($"sessions_{userId}") ?? new List<ChatSession>();
            return Json(sessions);
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetMessages(string sessionId)
        {
            var userId = _userManager.GetUserId(User);
            var messages = _cache.Get<List<ChatMessage>>($"msgs_{userId}_{sessionId}") ?? new List<ChatMessage>();
            return Json(messages);
        }

        [HttpPost]
        [Authorize]
        public IActionResult ResetChat()
        {
            var userId = _userManager.GetUserId(User);
            var sessionId = Request.Cookies["chatSessionId"];

            if (!string.IsNullOrEmpty(sessionId))
            {
                _geminiService.ResetChatHistory(userId, sessionId);
            }
            return Json(new { success = true, message = "Đã xóa sạch trí nhớ!" });
        }

        [HttpPost]
        [Authorize]
        public IActionResult SwitchSession(string sessionId)
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                Response.Cookies.Append("chatSessionId", sessionId, new CookieOptions { MaxAge = TimeSpan.FromDays(30) });
            }
            return Json(new { success = true });
        }
    }
}