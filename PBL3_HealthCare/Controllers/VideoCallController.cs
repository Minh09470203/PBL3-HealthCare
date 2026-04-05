using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.ViewModels; // Nhớ check lại namespace này
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize] // Bắt buộc phải đăng nhập mới được vào Controller này
    public class VideoCallController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public VideoCallController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        // =====================================
        // 1. GÁC CỔNG PHÒNG KHÁM VIDEO
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Room(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return NotFound("Không tìm thấy phòng khám.");

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.MeetingRoomId == roomId);

            if (appointment == null) return NotFound("Mã phòng không tồn tại.");

            var currentUser = await _userManager.GetUserAsync(User);

            // 🛑 CHẶN 1: BẢO MẬT CHÍNH CHỦ (Chỉ Bác sĩ hoặc Bệnh nhân của ca này mới được vào)
            bool isPatient = appointment.PatientId == currentUser.Id;
            bool isDoctor = appointment.Doctor.UserId == currentUser.Id;

            if (!isPatient && !isDoctor)
            {
                return Forbid(); // Đuổi cổ người lạ
            }

            // 🛑 CHẶN 2: KIỂM TRA THỜI GIAN (Chỉ cho vào trước 10 phút)
            DateTime appointmentTime = appointment.Date.Date.Add(appointment.TimeSlot);
            if (DateTime.Now < appointmentTime.AddMinutes(-10))
            {
                TempData["Error"] = $"Chưa đến giờ khám. Phòng sẽ mở vào lúc {appointmentTime.AddMinutes(-10):HH:mm}.";
                // Trả về lại trang dashboard tương ứng
                return isDoctor ? RedirectToAction("DoctorDashboard", "Home") : RedirectToAction("MyHistory", "Home");
            }

            // Đổi trạng thái sang InProgress nếu chưa đổi
            if (appointment.CallStatus == CallStatus.Pending)
            {
                appointment.CallStatus = CallStatus.InProgress;
                await _context.SaveChangesAsync();
            }

            // 🔑 CẤP "CHÌA KHÓA" ZEGOCLOUD CHO FRONTEND
            ViewBag.AppId = _configuration.GetValue<string>("ZegoCloud:AppId");
            ViewBag.ServerSecret = _configuration.GetValue<string>("ZegoCloud:ServerSecret");

            // Phân vai trò rõ ràng
            ViewBag.Role = isDoctor ? "Host" : "Audience";
            ViewBag.UserId = currentUser.Id;
            ViewBag.UserName = currentUser.FullName;
            ViewBag.RoomId = roomId;
            ViewBag.AppointmentId = appointment.Id; // Truyền ID này để lát gọi API FinishCall

            return View(); // Trả về Views/VideoCall/Room.cshtml cho Thái & Thịnh code
        }

        // =====================================
        // 2. API KẾT THÚC KHÁM & LƯU BỆNH ÁN
        // =====================================
        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> FinishCall([FromBody] FinishAppointmentViewModel model)
        {
            // 1. Kiểm tra dữ liệu đầu vào (giữ nguyên)
            if (model == null) return BadRequest(new { success = false, message = "Dữ liệu rỗng" });

            // 2. TÌM LỊCH HẸN: Phải Include đầy đủ Doctor và Patient
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)    // <--- Lôi ông Bác sĩ ra
                .Include(a => a.Patient)   // <--- Lôi ông Bệnh nhân ra
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null) return NotFound(new { success = false, message = "Không thấy lịch hẹn" });

            try
            {
                // 3. TẠO BỆNH ÁN MỚI: Gán trực tiếp Object thay vì ID
                var medicalRecord = new MedicalRecord
                {
                    AppointmentId = appointment.Id,

                    // 🔥 TẬN DỤNG THUỘC TÍNH OBJECT TẠI ĐÂY 🔥
                    Doctor = appointment.Doctor,           // Gán nguyên cả object Bác sĩ
                    ApplicationUser = appointment.Patient, // Gán nguyên cả object Bệnh nhân (để hiện bên Patient Portal)

                    Diagnosis = $"Triệu chứng: {model.Symptoms} | Chẩn đoán: {model.Diagnosis}",
                    Treatment = model.DoctorNotes ?? "Không có dặn dò",
                    CreatedAt = DateTime.Now
                };

                _context.MedicalRecords.Add(medicalRecord);

                // 4. Cập nhật trạng thái (giữ nguyên)
                appointment.Status = AppointmentStatus.Completed;
                appointment.CallStatus = CallStatus.Completed;
                _context.Update(appointment);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, medicalRecordId = medicalRecord.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi lưu DB: " + ex.Message });
            }
        }
    }
}