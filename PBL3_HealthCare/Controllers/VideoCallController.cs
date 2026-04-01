using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
using PBL3_HealthCare.ViewModels;
using System;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize]
    public class VideoCallController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ZegoTokenService _zegoTokenService;

        public VideoCallController(ApplicationDbContext context,
                                   UserManager<ApplicationUser> userManager,
                                   ZegoTokenService zegoTokenService)
        {
            _context = context;
            _userManager = userManager;
            _zegoTokenService = zegoTokenService;
        }

        //IActionResult Room(string roomId)
        public async Task<IActionResult> Room(string roomId)
        {
            if (string.IsNullOrEmpty(roomId) || !int.TryParse(roomId, out int appointmentId))
                return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // CHẶN 1: Nếu User KHÔNG PHẢI là Bệnh nhân/Bác sĩ của ca đó -> Forbid()
            bool isDoctor = appointment.Doctor.UserId == currentUserId;
            bool isPatient = appointment.PatientId == currentUserId;

            if (!isDoctor && !isPatient) return Forbid();

            // CHẶN 2: Nếu DateTime.Now < LịchKham.AddMinutes(-10) -> Báo lỗi "Chưa đến giờ"
            var appointmentTime = appointment.Date.Date.Add(appointment.TimeSlot);
            if (DateTime.Now < appointmentTime.AddMinutes(-10))
            {
                TempData["Error"] = "Chưa đến giờ khám. Vui lòng quay lại trước giờ hẹn 10 phút nhé ní!";
                return RedirectToAction("Index", "Home");
            }

            // HỢP LỆ -> Gọi ZegoTokenService lấy Token và ném xuống View
            int role = isDoctor ? 1 : 2; // 1: Host (Bác sĩ), 2: Audience (Bệnh nhân)
            string token = _zegoTokenService.GenerateToken(currentUserId, roomId, role);

            ViewBag.Token = token;
            ViewBag.Role = role;
            ViewBag.RoomId = roomId;
            ViewBag.AppId = 321894638;

            return View();
        }

        //API [HttpPost] FinishCall để đổi Status & Tạo bệnh án
        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> FinishCall([FromBody] FinishAppointmentViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var appointment = await _context.Appointments.FindAsync(model.AppointmentId);
            if (appointment == null) return NotFound();

            // Đổi trạng thái thành Completed
            appointment.Status = AppointmentStatus.Completed;

            // Thực hiện lệnh _context.MedicalRecords.Add(...) để tạo mới bệnh án
            var record = new MedicalRecord
            {
                AppointmentId = appointment.Id,
                DoctorId = appointment.DoctorId,
                Symptoms = model.Symptoms,
                Diagnosis = model.Diagnosis,
                DoctorNotes = model.DoctorNotes,
                CreatedAt = DateTime.Now
            };

            _context.MedicalRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã kết thúc ca khám và lưu bệnh án!" });
        }
    }
}