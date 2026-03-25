using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
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

namespace PBL3_HealthCare.Controllers
    {
        // Bùa bảo vệ: Phải là Admin mới được vào xem bảng lương, doanh thu!
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

                // 2. Đếm tổng số Bác sĩ (Lấy thẳng từ bảng Doctors cho lẹ)
                ViewBag.TotalDoctors = await _context.Doctors.CountAsync();

                // 3. Tính doanh thu THÁNG NÀY (Chỉ tính Hóa đơn đã thu tiền - Paid)
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                // Lưu ý: Đổi chữ InvoiceStatus.Paid thành Enum của sếp nếu đặt tên khác nhé
                ViewBag.MonthlyRevenue = await _context.Invoices
                    .Where(i => i.Status == InvoiceStatus.Paid
                            && i.CreatedAt.Month == currentMonth // SỬA Ở ĐÂY
                            && i.CreatedAt.Year == currentYear)
                    .SumAsync(i => i.TotalAmount);

                // Truyền sang cho Thái vẽ UI bằng ViewBag
                return View();
            }

            // ========================================================
            // 2. API TRẢ VỀ JSON: DÀNH CHO THÁI (FE 2) VẼ CHART.JS
            // ========================================================
            [HttpGet]
            public async Task<IActionResult> GetChartData()
            {
                // A. BIỂU ĐỒ TRÒN: Tỷ lệ bệnh nhân theo Chuyên khoa
                // Logic: Điếm số lượng Lịch khám (Appointment) gom nhóm theo Khoa của Bác sĩ
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

                // B. BIỂU ĐỒ CỘT: Doanh thu 6 tháng gần nhất
                // Khúc này hơi khoai, tui viết sẵn logic gom nhóm theo tháng cho sếp luôn
                var sixMonthsAgo = DateTime.Now.AddMonths(-5);
                var revenueStats = await _context.Invoices
                    .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1))
                    .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                    .Select(g => new
                    {
                        Month = g.Key.Month + "/" + g.Key.Year,
                        Total = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(r => r.Month) // Sắp xếp theo tháng
                    .ToListAsync();

                // Gói 2 cục data này thành dạng JSON quăng ra ngoài
                return Json(new
                {
                    PieChartData = specialtyStats,
                    BarChartData = revenueStats
                });
            }
        }
    }
}