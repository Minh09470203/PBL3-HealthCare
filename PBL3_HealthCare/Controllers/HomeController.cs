using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Query lấy 4 bác sĩ đầu tiên, Include bảng User và Specialty
            var topDoctors = await _context.Doctors
                                           .Include(d => d.User)
                                           .Include(d => d.Specialty)
                                           .Take(4)
                                           .ToListAsync();

            // truyền sang View
            return View(topDoctors);
        }

        // GET: /Home/BookAppointment (Gọi ra form điền)
        [HttpGet]
        public IActionResult BookAppointment()
        {
            // Gộp tên Bác sĩ và tên Khoa hiển thị cho đẹp (Vd: "Bs. Nguyễn Văn A - Răng Hàm Mặt")
            var doctorsList = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .Select(d => new {
                    Id = d.Id,
                    DisplayName = "Bs. " + d.User.FullName + " (" + d.Specialty.Name + ")"
                }).ToList();

            ViewData["DoctorId"] = new SelectList(doctorsList, "Id", "DisplayName");
            return View();
        }

        // POST: /Home/BookAppointment (Hứng data khách bấm nút Đặt)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment([Bind("DoctorId,Date,TimeSlot,Reason")] Appointment model)
        {
            if (ModelState.IsValid)
            {
                // 1. THUẬT TOÁN CHECK TRÙNG LỊCH (CỰC KỲ QUAN TRỌNG)
                bool isConflict = _context.Appointments.Any(a =>
                    a.DoctorId == model.DoctorId &&
                    a.Date == model.Date &&
                    a.TimeSlot == model.TimeSlot &&
                    a.Status != AppointmentStatus.Cancelled); // Nếu lịch cũ đã bị Hủy thì khách mới vẫn đặt được

                if (isConflict)
                {
                    // Bắn lỗi đỏ lòm ra giao diện cho khách biết
                    ModelState.AddModelError("", "Rất tiếc! Bác sĩ đã có lịch hẹn vào thời gian này. Vui lòng chọn giờ khác.");

                    // Load lại danh sách bác sĩ cho Dropdown để không bị lỗi màn hình trắng
                    var fallbackDoctors = _context.Doctors.Include(d => d.User).Include(d => d.Specialty)
                        .Select(d => new { Id = d.Id, DisplayName = "Bs. " + d.User.FullName + " (" + d.Specialty.Name + ")" }).ToList();
                    ViewData["DoctorId"] = new SelectList(fallbackDoctors, "Id", "DisplayName", model.DoctorId);

                    return View(model);
                }

                // 2. NẾU TRỐNG LỊCH -> LƯU VÀO DB
                var userId = _userManager.GetUserId(User);
                if (userId == null)
                {
                    // Chưa đăng nhập thì đá văng ra trang Login
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                model.PatientId = userId;
                model.Status = AppointmentStatus.Pending; // Mặc định là Chờ duyệt
                model.CreatedAt = DateTime.Now;

                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đặt lịch thành công! Vui lòng chờ phòng khám xác nhận.";
                return RedirectToAction(nameof(MyHistory)); // Đá thẳng sang trang Lịch sử
            }

            return View(model);
        }

        public async Task<IActionResult> MyHistory()
        {
            var userId = _userManager.GetUserId(User);

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialty)
                .Where(a => a.PatientId == userId)
                .ToListAsync();

            return View(appointments);
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


