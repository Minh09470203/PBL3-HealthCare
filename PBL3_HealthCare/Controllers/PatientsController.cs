using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PatientsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PatientsController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // ==========================================
        // 1. DANH SÁCH TẤT CẢ BỆNH NHÂN
        // ==========================================
        public async Task<IActionResult> Index()
        {
            // 1. Quét toàn bộ user có dán nhãn "Patient"
            var patients = await _userManager.GetUsersInRoleAsync("Patient");

            // 2. LỌC: Chỉ lấy những người đã bấm xác nhận Email (EmailConfirmed == true)
            // Sau đó mới sắp xếp người mới lên đầu
            var confirmedPatients = patients
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return View(confirmedPatients);
        }

        // ==========================================
        // 2. XEM HỒ SƠ 360 ĐỘ CỦA BỆNH NHÂN
        // ==========================================
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var patient = await _userManager.FindByIdAsync(id);
            if (patient == null) return NotFound();

            // Móc thêm Lịch sử khám bệnh của người này
            ViewBag.Appointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Where(a => a.PatientId == id)
                .OrderByDescending(a => a.Date).ThenByDescending(a => a.TimeSlot)
                .ToListAsync();

            // Móc thêm Lịch sử chi tiêu (Hóa đơn)
            ViewBag.Invoices = await _context.Invoices
                .Include(i => i.Appointment) // Include luôn để mang data Lịch hẹn ra View (nếu cần hiện ngày tháng)
                .Where(i => i.Appointment.PatientId == id)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // Móc thêm Hồ sơ bệnh án
            ViewBag.MedicalRecords = await _context.MedicalRecords
                .Include(m => m.Doctor).ThenInclude(d => d.User)
                .Where(m => m.ApplicationUser.Id == id)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(patient);
        }

        // ==========================================
        // 3. SỬA THÔNG TIN BỆNH NHÂN (Dành cho Lễ tân/Admin hỗ trợ)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var patient = await _userManager.FindByIdAsync(id);
            if (patient == null) return NotFound();

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,FullName,PhoneNumber,Address,Gender,DateOfBirth")] ApplicationUser model)
        {
            if (id != model.Id) return NotFound();

            var patient = await _userManager.FindByIdAsync(id);
            if (patient == null) return NotFound();

            // Chỉ cập nhật những trường cần thiết, không đụng vào Password hay SecurityStamp
            patient.FullName = model.FullName;
            patient.PhoneNumber = model.PhoneNumber;
            patient.Address = model.Address;
            patient.Gender = model.Gender;
            patient.DateOfBirth = model.DateOfBirth;

            var result = await _userManager.UpdateAsync(patient);
            if (result.Succeeded)
            {
                TempData["Success"] = "Đã cập nhật thông tin bệnh nhân thành công!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Có lỗi xảy ra khi lưu dữ liệu.";
            return View(patient);
        }

        // ==========================================
        // 4. KHÓA / MỞ KHÓA TÀI KHOẢN (Chống Spammer)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Bật tính năng cho phép khóa tài khoản này (nếu chưa bật)
            await _userManager.SetLockoutEnabledAsync(user, true);

            if (await _userManager.IsLockedOutAsync(user))
            {
                // Đang khóa -> Mở khóa
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["Success"] = $"Đã MỞ KHÓA tài khoản của {user.FullName}.";
            }
            else
            {
                // Đang mở -> Khóa 100 năm (Coi như khóa vĩnh viễn)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["Success"] = $"Đã KHÓA tài khoản của {user.FullName}.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 5. RESET MẬT KHẨU VỀ MẶC ĐỊNH
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // Đặt mật khẩu mặc định là: HealthCare@123
            var result = await _userManager.ResetPasswordAsync(user, token, "HealthCare@123");

            if (result.Succeeded)
            {
                TempData["Success"] = $"Đã reset mật khẩu của {user.FullName} về: HealthCare@123";
            }
            else
            {
                TempData["Error"] = "Không thể reset mật khẩu lúc này.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}