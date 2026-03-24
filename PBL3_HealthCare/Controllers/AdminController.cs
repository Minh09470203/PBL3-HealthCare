using Microsoft.AspNetCore.Mvc;

namespace PBL3_HealthCare.Controllers
{
    public class AdminController : Controller
    {
        // Trang Dashboard chính (Task 1)
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Print(int id)
        {
            // Thái phải chỉ định rõ đường dẫn vì file không nằm trong folder Views/Admin
            return View("~/Views/Invoices/Print.cshtml");
        }

    }
}