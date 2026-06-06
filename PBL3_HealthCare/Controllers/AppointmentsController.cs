using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.ViewModels;
using PBL3_HealthCare.Services;
using PBL3_HealthCare.Hubs;
using Microsoft.AspNetCore.SignalR; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Admin,Doctor")]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;
        private readonly EmailService _emailService; 
        private readonly IHubContext<NotificationHub> _hubContext;

        public AppointmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            EmailService emailService,
            IHubContext<NotificationHub> hubContext) 
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _emailService = emailService;
            _hubContext = hubContext;
        }

        // ==========================================
        // HÀM HỖ TRỢ: NẠP TÊN THẬT VÀO DROPDOWN
        // ==========================================
        private void PopulateNames(Appointment appointment = null)
        {
            var patients = _context.Users
                .Select(u => new { Id = u.Id, Name = u.FullName ?? u.UserName })
                .ToList();

            var doctors = _context.Doctors
                .Include(d => d.User)
                .Select(d => new { Id = d.Id, Name = "BS. " + d.User.FullName })
                .ToList();

            ViewData["PatientId"] = new SelectList(patients, "Id", "Name", appointment?.PatientId);
            ViewData["DoctorId"] = new SelectList(doctors, "Id", "Name", appointment?.DoctorId);
        }

        // ==========================================
        // INDEX
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Specialty)
                .Include(a => a.MedicalRecord).ThenInclude(m => m.Prescriptions)
                .AsQueryable();

            if (isDoctor && !isAdmin)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                if (doctor != null)
                {
                    query = query.Where(a => a.DoctorId == doctor.Id);
                }
            }

            var appointments = await query.OrderByDescending(a => a.Date).ToListAsync();
            return View(appointments);
        }

        // ==========================================
        // DETAILS
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        // ==========================================
        // CREATE
        // ==========================================
        public IActionResult Create()
        {
            PopulateNames();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // 🔥 ĐÃ THÊM IsVideoCall, MeetingRoomId VÀO BIND ĐỂ ADMIN CÓ THỂ TẠO LỊCH ONLINE THỦ CÔNG
        public async Task<IActionResult> Create([Bind("Id,PatientId,DoctorId,Date,Reason,Status,TimeSlot,Symptoms,CreatedAt,IsVideoCall,MeetingRoomId")] Appointment appointment)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            if (isDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                if (doctor != null) appointment.DoctorId = doctor.Id;

                appointment.Status = AppointmentStatus.Pending;
            }

            if (ModelState.IsValid)
            {
                // Kiểm tra xem khung giờ này đã bị ai đó đặt mất trong lúc Admin/Doctor đang thao tác form không
                bool isConflict = await _context.Appointments.AnyAsync(a =>
                    a.DoctorId == appointment.DoctorId &&
                    a.Date == appointment.Date.Date &&
                    a.TimeSlot == appointment.TimeSlot &&
                    a.Status != AppointmentStatus.Cancelled);

                if (isConflict)
                {
                    ModelState.AddModelError("", "Rất tiếc! Bác sĩ đã có lịch hẹn vào thời gian này. Vui lòng chọn khung giờ khác.");
                    PopulateNames(appointment);
                    return View(appointment);
                }

                appointment.CreatedAt = DateTime.Now;

                // Nếu là Online mà Admin quên sinh mã phòng
                if (appointment.IsVideoCall && string.IsNullOrEmpty(appointment.MeetingRoomId))
                {
                    appointment.MeetingRoomId = "ROOM-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    appointment.CallStatus = CallStatus.Pending;
                }

                _context.Add(appointment);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Tạo lịch khám thành công!";
                return RedirectToAction(nameof(Index));
            }

            PopulateNames(appointment);
            return View(appointment);
        }

        // ==========================================
        // WALK-IN BOOKING (Khách vãng lai)
        // ==========================================
        [HttpGet]
        public IActionResult WalkInBooking()
        {
            var doctors = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .Select(d => new { Id = d.Id, Name = "BS. " + d.User.FullName + " - " + d.Specialty.Name })
                .ToList();
            ViewData["DoctorId"] = new SelectList(doctors, "Id", "Name");
            return View(new WalkInViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WalkInBooking(WalkInViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra xem SĐT này đã có tài khoản chưa
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber || u.UserName == model.PhoneNumber);
                
                if (user == null)
                {
                    // Tạo tài khoản ngầm định cho bệnh nhân vãng lai
                    user = new ApplicationUser
                    {
                        UserName = model.PhoneNumber,
                        PhoneNumber = model.PhoneNumber,
                        FullName = model.PatientName,
                        Address = model.Address
                    };
                    
                    // Giả định email để không bị lỗi
                    await _userManager.SetEmailAsync(user, model.PhoneNumber + "@system.local");

                    // Tạo password ngầm định: SĐT + @123Aa
                    var result = await _userManager.CreateAsync(user, model.PhoneNumber + "@123Aa");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Patient");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Không thể tạo tài khoản ngầm định cho bệnh nhân này.");
                        var doctorsList = _context.Doctors.Include(d => d.User).Include(d => d.Specialty)
                            .Select(d => new { Id = d.Id, Name = "BS. " + d.User.FullName + " - " + d.Specialty.Name }).ToList();
                        ViewData["DoctorId"] = new SelectList(doctorsList, "Id", "Name", model.DoctorId);
                        return View(model);
                    }
                }

                // Kiểm tra xem khung giờ này đã bị ai đó đặt mất trong lúc Admin đang thao tác form không
                bool isConflict = await _context.Appointments.AnyAsync(a =>
                    a.DoctorId == model.DoctorId &&
                    a.Date == model.Date.Date &&
                    a.TimeSlot == model.TimeSlot &&
                    a.Status != AppointmentStatus.Cancelled);

                if (isConflict)
                {
                    ModelState.AddModelError("", "Rất tiếc! Bác sĩ đã có lịch hẹn vào thời gian này. Vui lòng chọn khung giờ khác.");
                    var doctorsList = _context.Doctors.Include(d => d.User).Include(d => d.Specialty)
                        .Select(d => new { Id = d.Id, Name = "BS. " + d.User.FullName + " - " + d.Specialty.Name }).ToList();
                    ViewData["DoctorId"] = new SelectList(doctorsList, "Id", "Name", model.DoctorId);
                    return View(model);
                }

                // Tạo lịch khám luôn được Confirmed
                var appointment = new Appointment
                {
                    PatientId = user.Id,
                    DoctorId = model.DoctorId,
                    Date = model.Date.Date,
                    TimeSlot = model.TimeSlot,
                    Reason = string.IsNullOrEmpty(model.Reason) ? "Khám trực tiếp tại quầy" : model.Reason,
                    Status = AppointmentStatus.Confirmed,
                    CreatedAt = DateTime.Now
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Tạo lịch khám thành công cho {model.PatientName}. Tài khoản đăng nhập đã được tạo (Tên: {model.PhoneNumber}, MK: {model.PhoneNumber}@123Aa).";
                return RedirectToAction(nameof(Index));
            }

            var doctors = _context.Doctors.Include(d => d.User).Include(d => d.Specialty)
                .Select(d => new { Id = d.Id, Name = "BS. " + d.User.FullName + " - " + d.Specialty.Name }).ToList();
            ViewData["DoctorId"] = new SelectList(doctors, "Id", "Name", model.DoctorId);
            return View(model);
        }

        // ==========================================
        // EDIT
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            PopulateNames(appointment);
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PatientId,DoctorId,Date,Reason,Status,TimeSlot,Symptoms,CreatedAt,IsVideoCall,MeetingRoomId")] Appointment appointment)
        {
            if (id != appointment.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(appointment);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật lịch khám thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateNames(appointment);
            return View(appointment);
        }

        // ==========================================
        // UPDATE STATUS (TRÁI TIM CỦA LUỒNG DUYỆT)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, AppointmentStatus newStatus)
        {
            // Include đầy đủ để lấy Email và Tên gửi mail
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            // 1. Cập nhật trạng thái
            appointment.Status = newStatus;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            // 2. Bắn thông báo nội bộ (Notification Service)
            string msg = newStatus == AppointmentStatus.Confirmed
                ? $"Lịch khám của bạn với BS. {appointment.Doctor.User.FullName} vào lúc {appointment.TimeSlot} ngày {appointment.Date:dd/MM/yyyy} đã được XÁC NHẬN."
                : $"Lịch khám ngày {appointment.Date:dd/MM/yyyy} của bạn đã BỊ HỦY.";

            await _notificationService.CreateNotification(appointment.PatientId, msg);
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", appointment.PatientId, msg);

            // 🔥 3. LOGIC GỬI EMAIL CHỨA LINK KHI ADMIN BẤM XÁC NHẬN LỊCH ONLINE 🔥
            if (newStatus == AppointmentStatus.Confirmed && appointment.IsVideoCall && appointment.Patient != null && !string.IsNullOrEmpty(appointment.Patient.Email))
            {
                try
                {
                    var request = HttpContext.Request;
                    var domain = $"{request.Scheme}://{request.Host}";
                    string roomUrl = $"{domain}/VideoCall/Room?roomId={appointment.MeetingRoomId}";

                    string emailSubject = "Xác nhận Lịch Khám Online - PBL3 HealthCare";
                    
                    // Đọc từ file HTML Template
                    string templatePath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "EmailTemplates", "ConfirmOnlineAppointment.html");
                    string emailBody = System.IO.File.ReadAllText(templatePath);
                    
                    // Điền dữ liệu thật vào các biến giả {{...}}
                    emailBody = emailBody.Replace("{{PatientName}}", appointment.Patient.FullName)
                                         .Replace("{{DoctorName}}", appointment.Doctor.User.FullName)
                                         .Replace("{{Date}}", appointment.Date.ToString("dd/MM/yyyy"))
                                         .Replace("{{TimeSlot}}", appointment.TimeSlot)
                                         .Replace("{{RoomUrl}}", roomUrl)
                                         .Replace("{{MeetingRoomId}}", appointment.MeetingRoomId);

                    // Gửi email không cần await hoặc bỏ qua lỗi timeout
                    _ = _emailService.SendEmailAsync(appointment.Patient.Email, emailSubject, emailBody);
                }
                catch (Exception ex)
                {
                    // Log lỗi gửi mail nếu cần, nhưng không làm chết app
                    TempData["Error"] = "Lịch đã duyệt nhưng không gửi được Email: " + ex.Message;
                }
            }

            TempData["Success"] = "Cập nhật trạng thái thành công!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appointment == null) return NotFound();
            return View(appointment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa lịch khám thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // LẤY LỊCH KHÁM CỦA BÁC SĨ BẰNG AJAX
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetDoctorSchedule(int doctorId)
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            DateTime vnToday = vnNow.Date;

            var availableSchedules = await _context.Schedules
                .Where(s => s.DoctorId == doctorId && s.Date >= vnToday && s.IsAvailable)
                .OrderBy(s => s.Date)
                .ToListAsync();

            var availableDates = availableSchedules.Select(s => s.Date.Date).Distinct().Take(5).ToList();
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
                .Where(a => a.DoctorId == doctorId &&
                            a.Date >= vnToday &&
                            a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            ViewBag.Next5Days = availableDates;
            ViewBag.TimeSlotsByDate = timeSlotsByDate;
            ViewBag.BookedAppointments = bookedAppointments;
            ViewBag.CurrentVnTime = vnNow;

            return PartialView("_DoctorSchedulePartial");
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.Id == id);
        }
    }
}