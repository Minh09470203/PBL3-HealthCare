using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ========================================================
        // 1. HÀM INDEX: HIỂN THỊ DASHBOARD
        // ========================================================
        public async Task<IActionResult> Index()
        {
            var patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.TotalPatients = patients.Count;

            ViewBag.TotalDoctors = await _context.Doctors.CountAsync();

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            // Tính doanh thu tháng hiện tại
            ViewBag.MonthlyRevenue = await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid
                            && i.CreatedAt.Month == currentMonth
                            && i.CreatedAt.Year == currentYear)
                .SumAsync(i => i.TotalAmount);

            return View();
        }

        // ========================================================
        // 2. HÀM PRINT: LẤY DỮ LIỆU THỰC TẾ ĐỂ IN
        // ========================================================
        public IActionResult Print(int id)
        {
            var invoice = _context.Invoices
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(i => i.Details)
                .FirstOrDefault(i => i.Id == id);

            if (invoice == null) return NotFound();

            return View("~/Views/Invoices/Print.cshtml", invoice);
        }

        // ========================================================
        // 3. API TRẢ VỀ JSON: ĐÃ FIX LỖI SQL GHÉP CHUỖI "/"
        // ========================================================
        [HttpGet]
        public async Task<IActionResult> GetChartData()
        {
            // A. Biểu đồ tròn: Thống kê chuyên khoa
            var specialtyStats = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialty)
                .Where(a => a.Doctor != null && a.Doctor.Specialty != null)
                .GroupBy(a => a.Doctor.Specialty.Name)
                .Select(g => new
                {
                    SpecialtyName = g.Key,
                    PatientCount = g.Count()
                })
                .ToListAsync();

            // B. Biểu đồ cột: Doanh thu 6 tháng (Fix lỗi Incorrect syntax)
            var sixMonthsAgo = DateTime.Now.AddMonths(-5);

            // Bước 1: Lấy dữ liệu thô từ SQL (Chỉ lấy số, không ghép chuỗi)
            var revenueDataRaw = await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid
                            && i.CreatedAt >= new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1))
                .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(i => i.TotalAmount)
                })
                .ToListAsync();

            // Bước 2: Định dạng chuỗi "Tháng/Năm" bằng C# (Xử lý trên RAM nên không lỗi SQL)
            var revenueStats = revenueDataRaw
                .Select(r => new
                {
                    Month = r.Month + "/" + r.Year,
                    Total = r.Total
                })
                .OrderBy(r => r.Month)
                .ToList();

            return Json(new
            {
                PieChartData = specialtyStats,
                BarChartData = revenueStats
            });
        }
    }
}