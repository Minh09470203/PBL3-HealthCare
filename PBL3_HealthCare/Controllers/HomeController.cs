using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
using PBL3_HealthCare.ViewModels;
using Microsoft.AspNetCore.SignalR; 
using PBL3_HealthCare.Hubs;         
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly NotificationService _notificationService;
        private readonly EmailService _emailService;
        private readonly IHubContext<NotificationHub> _hubContext; // 🔥 3. Khai báo biến đường ống

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            NotificationService notificationService,
            EmailService emailService,
            IHubContext<NotificationHub> hubContext) // 🔥 4. Tiêm nó vào hàm tạo
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
            _emailService = emailService;
            _hubContext = hubContext; // 🔥 5. Gán giá trị
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TopSpecialties = await _context.Specialties.Take(6).ToListAsync();
            if (User.Identity.IsAuthenticated)
            {
                // Kiểm tra xem ông này là Admin hay Bác sĩ
                if (User.IsInRole("Admin"))
                {
                    // Admin thì đá bay về trang quản lý chuyên khoa/dashboard
                    return RedirectToAction("Index", "Specialties");
                }
                else if (User.IsInRole("Doctor"))
                {
                    // Bác sĩ thì đá về trang lịch hẹn/lịch làm việc
                    return RedirectToAction("Index", "Appointments");
                }
            }
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
        // GET: /Home/SpecialtyList
        public async Task<IActionResult> SpecialtyList()
        {
            var specialties = await _context.Specialties
                .Include(s => s.Doctors)
                .ToListAsync();

            return View(specialties);
        }

        // 1. LẤY DANH SÁCH BÁC SĨ (CÓ LỌC KHOA)
        public async Task<IActionResult> DoctorList(int? specialtyId, bool isVideoCall = false)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .AsQueryable();

            if (specialtyId.HasValue)
            {
                query = query.Where(d => d.SpecialtyId == specialtyId);

                var specialty = await _context.Specialties
                    .Where(s => s.Id == specialtyId)
                    .FirstOrDefaultAsync();

                if (specialty != null)
                {
                    ViewBag.SpecialtyName = specialty.Name;
                    ViewBag.SpecialtyDescription = specialty.Description;
                    ViewBag.SpecialtyImage = specialty.Image;
                }
            }

            if (isVideoCall)
            {
                query = query.Where(d => d.IsVideoAvailable == true);
                ViewBag.IsVideoCall = true;
            }

            return View(await query.ToListAsync());
        }

        // 2. LẤY THÔNG TIN CHI TIẾT & BẢNG GIỜ KHÁM
        public async Task<IActionResult> DoctorInfo(int id, bool isVideoCall = false)
        {
            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (doctor == null) return NotFound();

            var availableSchedules = await _context.Schedules
                .Where(s => s.DoctorId == id && s.Date >= DateTime.Today && s.IsAvailable)
                .OrderBy(s => s.Date)
                .ToListAsync();

            var availableDates = availableSchedules.Select(s => s.Date.Date).Distinct().Take(3).ToList();
            var timeSlotsByDate = new Dictionary<DateTime, List<string>>();

            foreach (var date in availableDates)
            {
                var slotsForToday = new List<string>();
                var schedulesForToday = availableSchedules.Where(s => s.Date.Date == date).ToList();

                foreach (var schedule in schedulesForToday)
                {
                    if (schedule.Shift.Contains("Sáng") || schedule.Shift.Contains("Cả ngày"))
                        slotsForToday.AddRange(new[] { "08:00", "09:00", "10:00", "11:00" });

                    if (schedule.Shift.Contains("Chiều") || schedule.Shift.Contains("Cả ngày"))
                        slotsForToday.AddRange(new[] { "14:00", "15:00", "16:00" });

                    if (schedule.Shift.Contains("Tối"))
                        slotsForToday.AddRange(new[] { "18:00", "19:00", "20:00", "21:00", "22:00", "23:00" });
                }

                timeSlotsByDate[date] = slotsForToday.Distinct().OrderBy(t => t).ToList();
            }

            var bookedAppointments = await _context.Appointments
                .Where(a => a.DoctorId == id &&
                            a.Date >= DateTime.Today &&
                            a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            ViewBag.Next3Days = availableDates;
            ViewBag.TimeSlotsByDate = timeSlotsByDate;
            ViewBag.BookedAppointments = bookedAppointments;
            ViewBag.IsVideoCall = isVideoCall;

            return View(doctor);
        }

        // ==========================================
        // KHU VỰC 2: XỬ LÝ ĐẶT LỊCH (BOOKING)
        // ==========================================

        [HttpGet]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> BookAppointment(int? doctorId, DateTime? date, string timeSlot, bool isVideoCall = false)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (doctorId == null || date == null || string.IsNullOrEmpty(timeSlot))
            {
                return RedirectToAction("DoctorList", "Home");
            }

            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == doctorId);
            if (doctor == null) return NotFound();

            var model = new Appointment
            {
                DoctorId = doctorId.Value,
                Date = date.Value,
                TimeSlot = TimeSpan.Parse(timeSlot)
            };

            ViewBag.DoctorName = $"BS. {doctor.User.FullName}";
            ViewBag.DisplayDate = date.Value.ToString("dd/MM/yyyy");
            ViewBag.IsVideoCall = isVideoCall && doctor.IsVideoAvailable;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> BookAppointment([Bind("DoctorId,Date,TimeSlot,Reason,Symptoms,IsVideoCall")] Appointment model)
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

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                model.PatientId = user.Id;
                model.Status = AppointmentStatus.Pending;
                model.CreatedAt = DateTime.Now;

                if (model.IsVideoCall)
                {
                    model.MeetingRoomId = "ROOM-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    model.CallStatus = CallStatus.Pending;
                }

                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                // Bắn thông báo nội bộ cho bác sĩ
                var doctorInfo = await _context.Doctors.FindAsync(model.DoctorId);
                if (doctorInfo != null)
                {
                    string msg = $"Có bệnh nhân vừa đặt lịch khám với bạn vào lúc {model.TimeSlot} ngày {model.Date:dd/MM/yyyy}.";

                    // Lưu Database
                    await _notificationService.CreateNotification(doctorInfo.UserId, msg);

                    // 🔥 6. BẮN SÓNG REAL-TIME CHO BÁC SĨ NGAY LẬP TỨC 🔥
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", doctorInfo.UserId, msg);
                }

                TempData["Success"] = "Đặt lịch thành công! Vui lòng chờ phòng khám xác nhận.";
                return RedirectToAction(nameof(MyHistory));
            }

            return await ReloadViewOnError(model);
        }

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

            ViewBag.PackageBookings = await _context.PackageBookings
                .Include(p => p.HealthPackage)
                .Where(p => p.PatientId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(myAppointments);
        }

        // ==========================================
        // QUẢN LÝ HỒ SƠ BỆNH NHÂN (PROFILE)
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string FullName, string PhoneNumber, DateTime? DOB, string Gender, string Address, string Email, IFormFile AvatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            user.FullName = FullName;
            user.PhoneNumber = PhoneNumber;
            user.Gender = Gender;
            user.Address = Address;
            user.DateOfBirth = DOB;
            user.Email = Email;

            if (AvatarFile != null && AvatarFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(AvatarFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await AvatarFile.CopyToAsync(fileStream);
                }

                user.Avatar = uniqueFileName;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Cập nhật hồ sơ cá nhân thành công!";
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "Có lỗi xảy ra, không thể cập nhật hồ sơ!";
            return View(user);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu mới và xác nhận mật khẩu không khớp!";
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                TempData["Error"] = "Lỗi: " + error.Description;
                return View();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> DoctorProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound();

            return View(doctor);
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
        // ==========================================
        // QUẢN LÝ THÔNG BÁO (ALL NOTIFICATIONS)
        // ==========================================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> AllNotifications()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            // 1. Lấy toàn bộ thông báo của User này, sắp xếp mới nhất lên đầu
            var notifications = await _context.Notifications
                .Where(n => n.ReceiverId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // 2. Tự động "Đánh dấu đã đọc" cho những thông báo chưa đọc
            var unreadNotifs = notifications.Where(n => !n.IsRead).ToList();
            if (unreadNotifs.Any())
            {
                foreach (var notif in unreadNotifs)
                {
                    notif.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return View(notifications);
        }
        // ==========================================
        // CỔNG THÔNG TIN BỆNH NHÂN (PORTAL)
        // ==========================================

        public async Task<IActionResult> MyMedicalRecords()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var records = await _context.MedicalRecords
                .Include(m => m.Appointment)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User)
                .Where(m => m.Appointment.PatientId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(records);
        }

        public async Task<IActionResult> RecordDetails(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var record = await _context.MedicalRecords
                .Include(m => m.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(m => m.Doctor)
                    .ThenInclude(d => d.User)
                .Include(m => m.Prescriptions)
                    .ThenInclude(p => p.Details)
                        .ThenInclude(pd => pd.Medicine)
                .FirstOrDefaultAsync(m => m.Id == id && m.Appointment.PatientId == userId);

            if (record == null)
            {
                return NotFound("Không tìm thấy bệnh án hoặc bạn không có quyền xem dữ liệu này.");
            }

            return View(record);
        }

        public async Task<IActionResult> MyPrescriptions()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var prescriptions = await _context.Prescriptions
                .Include(p => p.MedicalRecord)
                    .ThenInclude(m => m.Doctor)
                        .ThenInclude(d => d.User)
                .Where(p => p.MedicalRecord.Appointment.PatientId == userId)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            return View(prescriptions);
        }

        public async Task<IActionResult> MyInvoices()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var invoices = await _context.Invoices
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.Doctor)
                        .ThenInclude(d => d.User)
                .Where(i => i.MedicalRecord.Appointment.PatientId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(invoices);
        }
    }
}