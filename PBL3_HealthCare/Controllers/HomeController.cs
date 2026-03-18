using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PBL3_HealthCare.Models;

namespace PBL3_HealthCare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Thêm dòng này để bắn thông báo sang file _AdminLayout.cshtml
            TempData["Success"] = "Chào Thái Leader! Hệ thống SweetAlert2 đã sẵn sàng hoạt động.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}