using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Data;
using Microsoft.EntityFrameworkCore;

namespace PBL3_HealthCare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // Lấy Top 4 bác sĩ từ database
            var topDoctors = _context.Doctors
                                     .Take(4)
                                     .ToList();

            // truyền sang View
            return View(topDoctors);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}