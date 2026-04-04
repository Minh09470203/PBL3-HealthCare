using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
using PBL3_HealthCare.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    // Chỉ Bác sĩ mới được vào "hang ổ" này
    [Authorize(Roles = "Doctor")]
    public class DoctorPortalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly NotificationService _notificationService;

        public DoctorPortalController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
        }

        // ==========================================
        // 1. DASHBOARD BÁC SĨ (Vừa vào là thấy lịch hôm nay)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index() // Đổi tên từ DoctorDashboard sang Index cho đúng chuẩn Portal
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound("Không tìm thấy hồ sơ Bác sĩ");

            // Lấy các lịch hẹn CHỈ TRONG HÔM NAY của bác sĩ này
            var todayAppointments = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.MedicalRecord)
                .Where(a => a.DoctorId == doctor.Id && a.Date.Date == DateTime.Today.Date)
                .OrderBy(a => a.TimeSlot)
                .ToListAsync();

            return View(todayAppointments);
        }

        // ==========================================
        // 2. QUẢN LÝ HỒ SƠ CÔNG TÁC (DOCTOR PROFILE)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Profile() // Đổi tên từ DoctorProfile sang Profile cho ngắn gọn
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(
            string FullName, string Email, string PhoneNumber, string Address,
            string Degree, decimal Price, string Bio,
            IFormFile AvatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound();

            // Cập nhật thông tin tài khoản
            user.FullName = FullName;
            user.Email = Email;
            user.PhoneNumber = PhoneNumber;
            user.Address = Address;
            await _userManager.UpdateAsync(user);

            // Cập nhật thông tin hành nghề
            doctor.Degree = Degree;
            doctor.Price = Price;
            doctor.Bio = Bio;

            if (AvatarFile != null && AvatarFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "doctors");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(AvatarFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await AvatarFile.CopyToAsync(fileStream);
                }

                // Xóa ảnh cũ
                if (!string.IsNullOrEmpty(doctor.Image))
                {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, "img", "doctors", doctor.Image);
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                doctor.Image = uniqueFileName;
            }

            _context.Update(doctor);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // ==========================================
        // 3. LỊCH LÀM VIỆC (Dùng cho FullCalendar)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> MySchedule()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

            var viewModel = new DoctorScheduleVM
            {
                // Lấy ca trực
                WorkShifts = await _context.Schedules
                    .Where(s => s.DoctorId == doctor.Id && s.Date >= DateTime.Today.AddDays(-7))
                    .ToListAsync(),

                // Lấy ca khám
                PatientAppointments = await _context.Appointments
                    .Include(a => a.Patient)
                    .Where(a => a.DoctorId == doctor.Id && a.Date >= DateTime.Today.AddDays(-7))
                    .ToListAsync()
            };

            return View(viewModel); // Truyền nguyên cục "Combo" này ra View
        }
    }
}