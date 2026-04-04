using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
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
        private readonly EmailService _emailService; // 🔥 ĐÃ THÊM

        public AppointmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            EmailService emailService) // 🔥 TIÊM EMAIL SERVICE VÀO
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _emailService = emailService;
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
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.MedicalRecord)
                .AsQueryable();

            if (isDoctor)
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

            // 🔥 3. LOGIC GỬI EMAIL CHỨA LINK KHI ADMIN BẤM XÁC NHẬN LỊCH ONLINE 🔥
            if (newStatus == AppointmentStatus.Confirmed && appointment.IsVideoCall && appointment.Patient != null && !string.IsNullOrEmpty(appointment.Patient.Email))
            {
                try
                {
                    var request = HttpContext.Request;
                    var domain = $"{request.Scheme}://{request.Host}";
                    string roomUrl = $"{domain}/VideoCall/Room?roomId={appointment.MeetingRoomId}";

                    string emailSubject = "Xác nhận Lịch Khám Online - PBL3 HealthCare";
                    string emailBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px; max-width: 600px;'>
                            <h2 style='color: #0d6efd;'>PBL3 HealthCare Clinic</h2>
                            <p>Chào <strong>{appointment.Patient.FullName}</strong>,</p>
                            <p>Lịch khám Online của bạn đã được <strong>XÁC NHẬN THÀNH CÔNG</strong> sau khi kiểm tra thông tin.</p>
                            <div style='background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                                <p style='margin: 5px 0;'><strong>Bác sĩ:</strong> BS. {appointment.Doctor.User.FullName}</p>
                                <p style='margin: 5px 0;'><strong>Ngày khám:</strong> {appointment.Date:dd/MM/yyyy}</p>
                                <p style='margin: 5px 0;'><strong>Giờ khám:</strong> {appointment.TimeSlot}</p>
                            </div>
                            <p style='color: #d63384; font-weight: bold;'>Hướng dẫn vào phòng:</p>
                            <p>Vui lòng chuẩn bị Camera, Micro và nhấn vào nút bên dưới để vào phòng khám <strong>trước giờ hẹn 10 phút</strong>.</p>
                            <a href='{roomUrl}' style='display: inline-block; padding: 12px 25px; background-color: #0d6efd; color: #ffffff; text-decoration: none; border-radius: 5px; font-weight: bold;'>BẤM VÀO ĐÂY ĐỂ VÀO PHÒNG KHÁM</a>
                            <p style='margin-top: 25px; font-size: 11px; color: #888; border-top: 1px solid #eee; padding-top: 10px;'>Mã phòng dự phòng: {appointment.MeetingRoomId}. Nếu không thể nhấn nút, hãy copy link sau dán vào trình duyệt: {roomUrl}</p>
                        </div>";

                    await _emailService.SendEmailAsync(appointment.Patient.Email, emailSubject, emailBody);
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

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.Id == id);
        }
    }
}