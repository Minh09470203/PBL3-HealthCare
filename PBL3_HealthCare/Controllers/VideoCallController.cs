using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace PBL3_HealthCare.Controllers
{
    [Authorize] // Chỉ người dùng đã đăng nhập mới vào được
    public class VideoCallController : Controller
    {
        // GET: /VideoCall/Room/5
        public IActionResult Room(int id)
        {
            ViewBag.AppID = 123456789; // Thay bằng AppID thực của nhóm trên ZegoCloud
            ViewBag.ServerSecret = "your_server_secret";
            ViewBag.RoomID = "Room_" + id;
            ViewBag.UserID = User.Identity.Name;
            ViewBag.UserName = User.Identity.Name;

            return View();
        }
    }
}