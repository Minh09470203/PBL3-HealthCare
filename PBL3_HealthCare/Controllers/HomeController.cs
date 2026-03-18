using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace PBL3_HealthCare.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Query lấy 4 bác sĩ đầu tiên, Include bảng User và Specialty
            var topDoctors = await _context.Doctors
                                           .Include(d => d.User)
                                           .Include(d => d.Specialty)
                                           .Take(4)
                                           .ToListAsync();

            // truyền sang View
            return View(topDoctors);
        }

        // GET: /Home/BookAppointment (Gọi ra form điền)
        [HttpGet]
        public IActionResult BookAppointment()
        {
            // Thêm dòng này để bắn thông báo sang file _AdminLayout.cshtml
            TempData["Success"] = "Chào Thái Leader! Hệ thống SweetAlert2 đã sẵn sàng hoạt động.";

            return View();
        }

        // POST: /Home/BookAppointment (Hứng data khách bấm nút Đặt)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment([Bind("DoctorId,Date,TimeSlot,Reason")] Appointment model)
        {
            if (ModelState.IsValid)
            {
                if (model.Date.Date < DateTime.Now.Date)
                {
                    ModelState.AddModelError("Date", "Lỗi: Không thể đặt lịch cho ngày trong quá khứ!");
                    return ReloadDropdownAndReturnView(model);
                }

                var doctorExists = await _context.Doctors.AnyAsync(d => d.Id == model.DoctorId);
                if (!doctorExists)
                {
                    ModelState.AddModelError("DoctorId", "Lỗi: Không tìm thấy hồ sơ Bác sĩ này!");
                    return ReloadDropdownAndReturnView(model);
                }

                // BÁC SĨ CÓ CA LÀM VIỆC VÀO NGÀY ĐÓ KHÔNG? (Giờ hợp lệ)
                var hasSchedule = await _context.Schedules.AnyAsync(s =>
                    s.DoctorId == model.DoctorId &&
                    s.Date.Date == model.Date.Date &&
                    s.IsAvailable == true);

                if (!hasSchedule)
                {
                    ModelState.AddModelError("Date", "Lỗi: Bác sĩ không có lịch trực hoặc đã nghỉ vào ngày này!");
                    return ReloadDropdownAndReturnView(model);
                }
                // 1. THUẬT TOÁN CHECK TRÙNG LỊCH (CỰC KỲ QUAN TRỌNG)
                bool isConflict = _context.Appointments.Any(a =>
                    a.DoctorId == model.DoctorId &&
                    a.Date == model.Date &&
                    a.TimeSlot == model.TimeSlot &&
                    a.Status != AppointmentStatus.Cancelled); // Nếu lịch cũ đã bị Hủy thì khách mới vẫn đặt được

                if (isConflict)
                {
                    ModelState.AddModelError("", "Rất tiếc! Bác sĩ đã có lịch hẹn vào thời gian này. Vui lòng chọn giờ khác.");
                    return ReloadDropdownAndReturnView(model);
                }

                // 2. NẾU TRỐNG LỊCH -> LƯU VÀO DB
                var userId = _userManager.GetUserId(User);
                if (userId == null)
                {
                    // Chưa đăng nhập thì đá văng ra trang Login
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                model.PatientId = userId;
                model.Status = AppointmentStatus.Pending; 
                model.CreatedAt = DateTime.Now;

                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đặt lịch thành công! Vui lòng chờ phòng khám xác nhận.";
                return RedirectToAction(nameof(MyHistory)); // Đá thẳng sang trang Lịch sử
            }

            return ReloadDropdownAndReturnView(model);
        }
        // HÀM HỖ TRỢ: Load lại danh sách Bác sĩ nếu form bị lỗi (tránh bị trắng trang)
        private IActionResult ReloadDropdownAndReturnView(Appointment model)
        {
            var fallbackDoctors = _context.Doctors.Include(d => d.User).Include(d => d.Specialty)
                .Select(d => new { Id = d.Id, DisplayName = "Bs. " + d.User.FullName + " (" + d.Specialty.Name + ")" }).ToList();

            ViewData["DoctorId"] = new SelectList(fallbackDoctors, "Id", "DisplayName", model.DoctorId);

            return View(model);
        }

        // GET: /Home/MyHistory
        [HttpGet]
        public async Task<IActionResult> MyHistory()
        {
            // Bắt buộc phải đăng nhập mới xem được
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // Lọc đúng Lịch khám của ông này, sắp xếp ngày mới nhất nổi lên đầu
            var myAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialty) // Kéo theo chuyên khoa để View có cái hiển thị
                .Where(a => a.PatientId == userId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            return View(myAppointments);
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
    }
}
