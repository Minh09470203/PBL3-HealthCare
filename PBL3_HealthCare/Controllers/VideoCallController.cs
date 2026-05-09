using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.ViewModels;
using PBL3_HealthCare.Services; // 🔥 THÊM DÒNG NÀY ĐỂ GỌI ZEGOTOKENSERVICE
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
        private readonly ZegoTokenService _zegoTokenService; // 🔥 KHAI BÁO SERVICE

        // 🔥 TIÊM SERVICE VÀO CONSTRUCTOR
        public VideoCallController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ZegoTokenService zegoTokenService)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _zegoTokenService = zegoTokenService;
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

            // 🛑 CHẶN 1: BẢO MẬT CHÍNH CHỦ
            bool isPatient = appointment.PatientId == currentUser.Id;
            bool isDoctor = appointment.Doctor.UserId == currentUser.Id;

            if (!isPatient && !isDoctor)
            {
                return Forbid();
            }

            // 🔥 FIX: ÉP MÚI GIỜ VIỆT NAM CHO SERVER QUỐC TẾ 🔥
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            DateTime vnNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

            // 🛑 CHẶN 2: KIỂM TRA THỜI GIAN (DÙNG GIỜ VN)
            DateTime appointmentTime = appointment.Date.Date.Add(appointment.TimeSlot);
            if (vnNow < appointmentTime.AddMinutes(-10)) // Thay DateTime.Now bằng vnNow
            {
                TempData["Error"] = $"Chưa đến giờ khám. Phòng sẽ mở vào lúc {appointmentTime.AddMinutes(-10):HH:mm}.";
                return isDoctor ? RedirectToAction("DoctorDashboard", "Home") : RedirectToAction("MyHistory", "Home");
            }

            // ✅ CHẶN 3: PHÒNG ĐÃ ĐÓNG (DÙNG GIỜ VN)
            DateTime closeTime = appointmentTime.AddMinutes(45);
            if (vnNow > closeTime) // Thay DateTime.Now bằng vnNow
            {
                TempData["Error"] = $"Phiên khám đã kết thúc lúc {closeTime:HH:mm}. " +
                                    $"Vui lòng đặt lịch mới nếu cần tư vấn thêm.";
                return isDoctor
                    ? RedirectToAction("Index", "DoctorPortal")
                    : RedirectToAction("MyHistory", "Home");
            }

            // Đổi trạng thái sang InProgress
            if (appointment.CallStatus == CallStatus.Pending)
            {
                appointment.CallStatus = CallStatus.InProgress;
                await _context.SaveChangesAsync();
            }

            // 🔑 SINH TOKEN BẢO MẬT
            string secureToken = _zegoTokenService.GenerateToken(currentUser.Id, roomId);

            ViewBag.AppId = _configuration.GetValue<uint>("ZegoCloud:AppId");
            ViewBag.ZegoToken = secureToken;

            ViewBag.Role = isDoctor ? "Host" : "Audience";
            ViewBag.UserId = currentUser.Id;
            ViewBag.UserName = currentUser.FullName;
            ViewBag.RoomId = roomId;
            ViewBag.AppointmentId = appointment.Id;

            return View();
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
                    ReExaminationDate = model.ReExaminationDate,
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