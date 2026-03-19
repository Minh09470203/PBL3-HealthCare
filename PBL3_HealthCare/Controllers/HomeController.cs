using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;

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
            ViewBag.TopSpecialties = await _context.Specialties.Take(6).ToListAsync();
            // Query lấy 4 bác sĩ đầu tiên, Include bảng User và Specialty
            var doctors = await _context.Doctors
                                           .Include(d => d.User)
                                           .Include(d => d.Specialty)
                                           .Take(4)
                                           .ToListAsync();
            var viewModel = new HomeViewModel
            {
                TopDoctors = doctors,
                AllDoctors = doctors
            };

            return View(viewModel);
        }

        // ==========================================
        // KHU VỰC 1: LUỒNG TÌM KIẾM BÁC SĨ & LỊCH KHÁM
        // ==========================================

        // 1. LẤY DANH SÁCH BÁC SĨ (CÓ LỌC KHOA)
        public async Task<IActionResult> DoctorList(int? specialtyId)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .AsQueryable();

            if (specialtyId.HasValue)
            {
                query = query.Where(d => d.SpecialtyId == specialtyId);
                ViewBag.SpecialtyName = await _context.Specialties
                    .Where(s => s.Id == specialtyId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();
            }

            return View(await query.ToListAsync());
        }

        // 2. LẤY HỒ SƠ CHI TIẾT & BẢNG GIỜ KHÁM
        public async Task<IActionResult> DoctorProfile(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (doctor == null) return NotFound();

            // Khởi tạo danh sách giờ và 3 ngày tới
            var timeSlots = new List<string> { "08:00", "09:00", "10:00", "14:00", "15:00", "16:00" };
            var next3Days = new List<DateTime> { DateTime.Now.Date, DateTime.Now.Date.AddDays(1), DateTime.Now.Date.AddDays(2) };

            // Lấy danh sách lịch ĐÃ CÓ NGƯỜI ĐẶT
            var bookedAppointments = await _context.Appointments
                .Where(a => a.DoctorId == id &&
                            a.Date >= DateTime.Now.Date &&
                            a.Date <= DateTime.Now.Date.AddDays(2) &&
                            a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            ViewBag.Next3Days = next3Days;
            ViewBag.TimeSlots = timeSlots;
            ViewBag.BookedAppointments = bookedAppointments;

            return View(doctor);
        }

        // ==========================================
        // KHU VỰC 2: XỬ LÝ ĐẶT LỊCH (BOOKING)
        // ==========================================

        // GET: /Home/BookAppointment (Hứng data từ DoctorProfile)
        [HttpGet]
        public async Task<IActionResult> BookAppointment(int? doctorId, DateTime? date, string timeSlot)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (doctorId == null || date == null || string.IsNullOrEmpty(timeSlot))
            {
                TempData["Error"] = "Vui lòng chọn Khoa và Bác sĩ trước khi đặt lịch!";
                return RedirectToAction("DoctorList", "Home");
            }

            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == doctorId);
            if (doctor == null) return NotFound();

            // Khởi tạo Model để chống lỗi NullReferenceException
            var model = new Appointment
            {
                DoctorId = doctorId.Value,
                Date = date.Value,
                TimeSlot = TimeSpan.Parse(timeSlot)
            };

            ViewBag.DoctorName = $"BS. {doctor.User.FullName}";
            ViewBag.DisplayDate = date.Value.ToString("dd/MM/yyyy");

            return View(model);
        }

        // POST: /Home/BookAppointment (Xử lý lưu vào Database)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment([Bind("DoctorId,Date,TimeSlot,Reason")] Appointment model)
        {
            ModelState.Remove("PatientId");
            ModelState.Remove("Status");

            if (ModelState.IsValid)
            {
                if (model.Date.Date < DateTime.Now.Date)
                {
                    ModelState.AddModelError("Date", "Lỗi: Không thể đặt lịch cho ngày trong quá khứ!");
                    return await ReloadViewOnError(model);
                }

                var doctorExists = await _context.Doctors.AnyAsync(d => d.Id == model.DoctorId);
                if (!doctorExists)
                {
                    ModelState.AddModelError("DoctorId", "Lỗi: Không tìm thấy hồ sơ Bác sĩ này!");
                    return await ReloadViewOnError(model);
                }

                // THUẬT TOÁN CHECK TRÙNG LỊCH (Chặn nếu có người nhanh tay đặt trước)
                bool isConflict = await _context.Appointments.AnyAsync(a =>
                    a.DoctorId == model.DoctorId &&
                    a.Date == model.Date &&
                    a.TimeSlot == model.TimeSlot &&
                    a.Status != AppointmentStatus.Cancelled);

                if (isConflict)
                {
                    ModelState.AddModelError("", "Rất tiếc! Bác sĩ đã có lịch hẹn vào thời gian này. Vui lòng chọn giờ khác.");
                    return await ReloadViewOnError(model);
                }

                // LƯU VÀO DB
                var userId = _userManager.GetUserId(User);
                if (userId == null)
                {
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                model.PatientId = userId;
                model.Status = AppointmentStatus.Pending;
                model.CreatedAt = DateTime.Now;

                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đặt lịch thành công! Vui lòng chờ phòng khám xác nhận.";
                return RedirectToAction(nameof(MyHistory));
            }

            return await ReloadViewOnError(model);
        }

        // Hàm hỗ trợ nạp lại thông tin nếu Form bị lỗi (Chống màn hình trắng)
        private async Task<IActionResult> ReloadViewOnError(Appointment model)
        {
            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == model.DoctorId);
            ViewBag.DoctorName = doctor != null ? $"BS. {doctor.User.FullName}" : "Đang cập nhật";
            ViewBag.DisplayDate = model.Date.ToString("dd/MM/yyyy");
            return View("BookAppointment", model);
        }

        // ==========================================
        // KHU VỰC 3: CÁC TRANG CÒN LẠI
        // ==========================================

        // GET: /Home/MyHistory
        [HttpGet]
        public async Task<IActionResult> MyHistory()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var myAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialty)
                .Where(a => a.PatientId == userId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            return View(myAppointments);
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