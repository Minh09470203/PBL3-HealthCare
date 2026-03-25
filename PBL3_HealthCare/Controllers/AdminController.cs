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
    // Bùa bảo vệ: Chỉ Admin mới được vào xem bảng lương, doanh thu!
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
        // 1. HÀM INDEX: LOAD GIAO DIỆN & CÁC CON SỐ TỔNG QUAN
        // ========================================================
        public async Task<IActionResult> Index()
        {
            // 1. Đếm tổng số Bệnh nhân (Quét qua hệ thống Identity)
            var patients = await _userManager.GetUsersInRoleAsync("Patient");
            ViewBag.TotalPatients = patients.Count;

            // 2. Đếm tổng số Bác sĩ
            ViewBag.TotalDoctors = await _context.Doctors.CountAsync();

            // 3. Tính doanh thu THÁNG NÀY (Chỉ tính Hóa đơn đã thu tiền - Paid)
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            ViewBag.MonthlyRevenue = await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid
                            && i.CreatedAt.Month == currentMonth
                            && i.CreatedAt.Year == currentYear)
                .SumAsync(i => i.TotalAmount);

            return View();
        }

        // ========================================================
        // 2. HÀM PRINT: LẤY DỮ LIỆU THẬT ĐỂ IN HÓA ĐƠN
        // ========================================================
        public IActionResult Print(int id)
        {
            // Lấy hóa đơn kèm thông tin Bệnh nhân (qua Appointment) và Chi tiết thuốc (Details)
            var invoice = _context.Invoices
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(i => i.Details) // Đã sửa theo tên "Details" trong Model của Thái
                .FirstOrDefault(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            // Chỉ định rõ đường dẫn file Print.cshtml trong folder Invoices
            return View("~/Views/Invoices/Print.cshtml", invoice);
        }

        // ========================================================
        // 3. API TRẢ VỀ JSON: DÀNH CHO THÁI VẼ CHART.JS
        // ========================================================
        [HttpGet]
        public async Task<IActionResult> GetChartData()
        {
            // A. BIỂU ĐỒ TRÒN: Tỷ lệ bệnh nhân theo Chuyên khoa
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

            // B. BIỂU ĐỒ CỘT: Doanh thu 6 tháng gần nhất (ĐÃ FIX LỖI SQL)
            var sixMonthsAgo = DateTime.Now.AddMonths(-5);

            // Bước 1: Lấy dữ liệu thô từ Database (Không ghép chuỗi ở đây)
            var rawData = await _context.Invoices
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

            // Bước 2: Định dạng lại chuỗi hiển thị bằng C# trên bộ nhớ RAM
            var revenueStats = rawData.Select(r => new
            {
                Month = r.Month + "/" + r.Year,
                Total = r.Total
            }).ToList();

            return Json(new
            {
                PieChartData = specialtyStats,
                BarChartData = revenueStats
            });
        }
    }
}