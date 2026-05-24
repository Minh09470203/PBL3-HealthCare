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
        private readonly EmailService _emailService;

        public DoctorPortalController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            NotificationService notificationService,
            EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
            _emailService = emailService;
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
                    .ThenInclude(m => m.Prescriptions)
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
            string FullName, string PhoneNumber, string Address,
            string Degree, decimal Price, string Bio, string Email,
            IFormFile AvatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound();

            // Cập nhật thông tin tài khoản
            user.FullName = FullName;
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

            // Xử lý Email
            bool emailChanged = false;
            if (!string.IsNullOrEmpty(Email) && user.Email != Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(Email);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    TempData["Error"] = "Email này đã được sử dụng bởi một tài khoản khác!";
                    return RedirectToAction(nameof(Profile));
                }
                
                var code = await _userManager.GenerateChangeEmailTokenAsync(user, Email);
                code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmailChange",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, email = Email, code = code },
                    protocol: Request.Scheme);

                string mailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #f4f7f6;'>
                    <div style='max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; border-top: 5px solid #3d5ee1;'>
                        <h2 style='color: #3d5ee1; text-align: center;'>XÁC THỰC EMAIL MỚI</h2>
                        <p>Chào bác sĩ <strong>{user.FullName ?? "bạn"}</strong>,</p>
                        <p>Vui lòng bấm vào nút bên dưới để xác nhận đổi email của bạn:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(callbackUrl)}' style='background-color: #3d5ee1; color: white; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-weight: bold;'>XÁC THỰC EMAIL</a>
                        </div>
                    </div>
                </div>";

                await _emailService.SendEmailAsync(Email, "Xác nhận thay đổi email - SuperStar", mailBody);
                emailChanged = true;
            }

            _context.Update(doctor);
            await _context.SaveChangesAsync();

            if (emailChanged)
            {
                TempData["Success"] = "Cập nhật hồ sơ thành công! Vui lòng kiểm tra hộp thư email để xác thực email mới.";
            }
            else
            {
                TempData["Success"] = "Cập nhật hồ sơ thành công!";
            }
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