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
        [Authorize(Roles = "Doctor")] // Chỉ bác sĩ mới được chốt sổ
        public async Task<IActionResult> FinishCall([FromBody] FinishAppointmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Dữ liệu nhập vào không hợp lệ.");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null) return NotFound("Không tìm thấy ca khám.");

            try
            {
                // 1. TẠO BỆNH ÁN MỚI TỪ DỮ LIỆU VIDEO CALL
                var medicalRecord = new MedicalRecord
                {
                    AppointmentId = appointment.Id,
                    Diagnosis = $"Triệu chứng: {model.Symptoms} \nChẩn đoán: {model.Diagnosis}",
                    Treatment = model.DoctorNotes,
                    CreatedAt = DateTime.Now
                };

                _context.MedicalRecords.Add(medicalRecord);

                // Lưu bệnh án trước để lấy được MedicalRecord ID (phục vụ nếu có thêm đơn thuốc)
                await _context.SaveChangesAsync();

                // 2. ĐỔI TRẠNG THÁI LỊCH HẸN THÀNH COMPLETED
                appointment.Status = AppointmentStatus.Completed; // Trạng thái chung
                appointment.CallStatus = CallStatus.Completed;   // Trạng thái của video call

                _context.Update(appointment);
                await _context.SaveChangesAsync();

                // (Tùy chọn): Nếu sếp có bảng Đơn thuốc riêng, thì xử lý model.Prescription ở đây

                // 🔥 ĐIỂM ĂN TIỀN NHẤT LÀ ĐÂY: Trả về medicalRecordId cho Javascript bẻ lái 🔥
                return Ok(new { 
                    success = true, 
                    message = "Lưu bệnh án thành công!",
                    medicalRecordId = medicalRecord.Id // Đẻ xong ID là ném ra ngoài luôn
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi Server: " + ex.Message);
            }
        }
    }
}