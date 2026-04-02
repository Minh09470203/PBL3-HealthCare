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
        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            NotificationService notificationService,
            EmailService emailService)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
            _emailService = emailService;
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
            // Lấy toàn bộ danh sách khoa
            // Mình Include thêm Doctors ở đây để phòng hờ lát nữa ra View, 
            // bạn muốn hiển thị dòng chữ kiểu "Có 5 bác sĩ" dưới mỗi thẻ khoa cho đẹp.
            var specialties = await _context.Specialties
                .Include(s => s.Doctors)
                .ToListAsync();

            return View(specialties);
        }

        // 1. LẤY DANH SÁCH BÁC SĨ (CÓ LỌC KHOA)
        public async Task<IActionResult> DoctorList(int? specialtyId)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .AsQueryable();

            if (specialtyId.HasValue)
            {
                query = query.Where(d => d.SpecialtyId == specialtyId);

                // Sửa lại đoạn này: Lấy toàn bộ object Specialty thay vì chỉ lấy Name
                var specialty = await _context.Specialties
                    .Where(s => s.Id == specialtyId)
                    .FirstOrDefaultAsync();

                if (specialty != null)
                {
                    // Truyền tất cả thông tin cần thiết qua ViewBag
                    ViewBag.SpecialtyName = specialty.Name;
                    ViewBag.SpecialtyDescription = specialty.Description;
                    ViewBag.SpecialtyImage = specialty.Image;
                }
            }

            return View(await query.ToListAsync());
        }

        // 2. LẤY THÔNG TIN CHI TIẾT & BẢNG GIỜ KHÁM (Dựa vào Schedule)
        public async Task<IActionResult> DoctorInfo(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (doctor == null) return NotFound();

            // 1. LẤY DANH SÁCH CA LÀM VIỆC TỪ BẢNG SCHEDULE
            // Lấy từ hôm nay trở đi và chỉ lấy những ca đang Mở (IsAvailable == true)
            var availableSchedules = await _context.Schedules
                .Where(s => s.DoctorId == id && s.Date >= DateTime.Today && s.IsAvailable)
                .OrderBy(s => s.Date)
                .ToListAsync();

            // Trích xuất ra danh sách các NGÀY để in ra màn hình cho bệnh nhân chọn
            // Dùng Distinct() để lỡ 1 ngày bác sĩ trực 2 ca thì màn hình vẫn chỉ hiện 1 nút chọn ngày
            var availableDates = availableSchedules.Select(s => s.Date.Date).Distinct().Take(3).ToList();

            // 2. TẠO TỪ ĐIỂN KHUNG GIỜ RIÊNG BIỆT CHO TỪNG NGÀY
            var timeSlotsByDate = new Dictionary<DateTime, List<string>>();

            foreach (var date in availableDates)
            {
                var slotsForToday = new List<string>();

                // Chỉ lấy các ca làm việc của đúng cái ngày đang xét
                var schedulesForToday = availableSchedules.Where(s => s.Date.Date == date).ToList();

                foreach (var schedule in schedulesForToday)
                {
                    if (schedule.Shift.Contains("Sáng") || schedule.Shift.Contains("Cả ngày"))
                        slotsForToday.AddRange(new[] { "08:00", "09:00", "10:00", "11:00" });

                    if (schedule.Shift.Contains("Chiều") || schedule.Shift.Contains("Cả ngày"))
                        slotsForToday.AddRange(new[] { "14:00", "15:00", "16:00" });

                    if (schedule.Shift.Contains("Tối"))
                        slotsForToday.AddRange(new[] { "18:00", "19:00", "20:00" });
                }

                // Lọc trùng, sắp xếp và cất vào "ngăn kéo" của ngày đó
                timeSlotsByDate[date] = slotsForToday.Distinct().OrderBy(t => t).ToList();
            }

            // 3. TÌM NHỮNG GIỜ ĐÃ BỊ ĐẶT MẤT ĐỂ LÀM MỜ NÚT BẤM
            var bookedAppointments = await _context.Appointments
                .Where(a => a.DoctorId == id &&
                            a.Date >= DateTime.Today &&
                            a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            // Truyền dữ liệu ra View
            ViewBag.Next3Days = availableDates;
            ViewBag.TimeSlotsByDate = timeSlotsByDate; // Gửi cả cái Từ điển ra ngoài
            ViewBag.BookedAppointments = bookedAppointments;

            return View(doctor);
        }

        // ==========================================
        // KHU VỰC 2: XỬ LÝ ĐẶT LỊCH (BOOKING)
        // ==========================================

        // GET: /Home/BookAppointment (Hứng data từ DoctorProfile)
        // GET: /Home/BookAppointment (Hứng data từ DoctorProfile)
        [HttpGet]
        [Authorize(Roles = "Patient")]
        // Sếp THÊM tham số isVideoCall vào đây nhé:
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

            // DÒNG NÀY RẤT QUAN TRỌNG: Check xem url có chữ isVideoCall=true không, VÀ bác sĩ này có nhận khám online không!
            ViewBag.IsVideoCall = isVideoCall && doctor.IsVideoAvailable;

            return View(model);
        }

        // POST: /Home/BookAppointment (Xử lý lưu vào Database)
        // POST: /Home/BookAppointment (Xử lý lưu vào Database)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Patient")]
        // NHỚ THÊM IsVideoCall VÀO DÒNG BIND DƯỚI ĐÂY:
        public async Task<IActionResult> BookAppointment([Bind("DoctorId,Date,TimeSlot,Reason,IsVideoCall")] Appointment model)
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

                // Check trùng lịch
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

                // Lấy thông tin user
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToPage("/Account/Login", new { area = "Identity" });
                }

                // LƯU VÀO DB
                model.PatientId = user.Id;
                model.Status = AppointmentStatus.Pending;
                model.CreatedAt = DateTime.Now;

                // 🔥 LOGIC KHÁM VIDEO Ở ĐÂY 🔥
                if (model.IsVideoCall)
                {
                    // Sinh mã phòng ngẫu nhiên 8 ký tự
                    model.MeetingRoomId = "ROOM-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                    // Đặt CallStatus cho phòng gọi (enum mà sếp vừa tạo đó)
                    model.CallStatus = CallStatus.Pending;
                }

                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                // Bắn thông báo nội bộ cho bác sĩ
                var doctorInfo = await _context.Doctors.FindAsync(model.DoctorId);
                if (doctorInfo != null)
                {
                    await _notificationService.CreateNotification(
                        doctorInfo.UserId,
                        $"Có bệnh nhân vừa đặt lịch khám với bạn vào lúc {model.TimeSlot} ngày {model.Date:dd/MM/yyyy}."
                    );
                }

                // 🔥 GỬI EMAIL CHỨA LINK PHÒNG KHÁM CHO BỆNH NHÂN 🔥
                if (model.IsVideoCall && !string.IsNullOrEmpty(user.Email))
                {
                    // Tự động lấy domain hiện tại (http://localhost:xxxx) để tạo link
                    var request = HttpContext.Request;
                    var domain = $"{request.Scheme}://{request.Host}";
                    string roomUrl = $"{domain}/VideoCall/Room?roomId={model.MeetingRoomId}";

                    string emailSubject = "Xác nhận Lịch Khám Qua Video - PBL3 HealthCare";
                    string emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                    <h2 style='color: #0d6efd;'>PBL3 HealthCare Clinic</h2>
                    <p>Chào <strong>{user.FullName}</strong>,</p>
                    <p>Lịch khám Online của bạn đã được xác nhận thành công. Thông tin chi tiết:</p>
                    <ul>
                        <li><strong>Bác sĩ:</strong> BS. {ViewBag.DoctorName ?? doctorInfo?.UserId}</li>
                        <li><strong>Ngày khám:</strong> {model.Date:dd/MM/yyyy}</li>
                        <li><strong>Giờ khám:</strong> {model.TimeSlot}</li>
                    </ul>
                    <p style='color: red;'><strong>Lưu ý:</strong> Vui lòng chuẩn bị Camera, Micro và truy cập vào link bên dưới <strong>trước giờ hẹn 10 phút</strong>.</p>
                    <a href='{roomUrl}' style='display: inline-block; padding: 12px 20px; margin-top: 10px; background-color: #0d6efd; color: #ffffff; text-decoration: none; border-radius: 5px; font-weight: bold;'>BẤM VÀO ĐÂY ĐỂ VÀO PHÒNG KHÁM</a>
                    <p style='margin-top: 20px; font-size: 12px; color: #888;'>Mã phòng dự phòng của bạn là: {model.MeetingRoomId}</p>
                </div>";

                    await _emailService.SendEmailAsync(user.Email, emailSubject, emailBody);
                }

                TempData["Success"] = "Đặt lịch thành công! Vui lòng chờ phòng khám xác nhận.";
                return RedirectToAction(nameof(MyHistory));
            }

            return await ReloadViewOnError(model);
        }

        // Hàm hỗ trợ nạp lại thông tin nếu Form bị lỗi (Chống màn hình trắng)
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

        // GET: /Home/MyHistory
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

        // GET: /Home/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Lấy thông tin người dùng đang đăng nhập
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return View(user); // Truyền thẳng object User ra View
        }

        // POST: /Home/Profile (Xử lý khi bấm nút Lưu)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string FullName, string PhoneNumber, DateTime? DOB, string Gender, string Address, string Email, IFormFile AvatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // Cập nhật các trường thông tin
            user.FullName = FullName;
            user.PhoneNumber = PhoneNumber;
            user.Gender = Gender;
            user.Address = Address;
            user.DateOfBirth = DOB;
            user.Email = Email;

            if (AvatarFile != null && AvatarFile.Length > 0)
            {
                // 1. Trỏ đường dẫn tới thư mục wwwroot/images/ (hoặc wwwroot/images/doctors)
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");

                // 2. Tạo tên file duy nhất (Chống trùng tên bằng Guid)
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(AvatarFile.FileName);

                // 3. Đường dẫn lưu file vật lý trên máy
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // 4. Copy file từ luồng (stream) vào ổ cứng
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await AvatarFile.CopyToAsync(fileStream);
                }

                // 5. Cập nhật tên file vào thuộc tính của Model để lưu xuống Database (cột Image/Avatar)
                user.Avatar = uniqueFileName; // Đảm bảo trong bảng User hoặc Doctor ông có cột này
            }
            // LƯU Ý CHO MINH: Nếu trong bảng ApplicationUser của ông có thêm các cột 
            // như Address (Địa chỉ), DOB (Ngày sinh)... thì ông bổ sung thêm tham số 
            // vào hàm này và gán giá trị ở đây nhé. Ví dụ: user.Address = Address;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Cập nhật hồ sơ cá nhân thành công!";
                return RedirectToAction(nameof(Profile));
            }

            TempData["Error"] = "Có lỗi xảy ra, không thể cập nhật hồ sơ!";
            return View(user);
        }

        // GET: /Home/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Home/ChangePassword
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
                // Đổi pass xong đá về trang Profile
                return RedirectToAction(nameof(Profile));
            }

            // Nếu mật khẩu cũ sai hoặc pass mới không đủ độ khó (chưa có chữ hoa, số...)
            foreach (var error in result.Errors)
            {
                TempData["Error"] = "Lỗi: " + error.Description;
                return View();
            }

            return View();
        }

        // ==========================================
        // HỒ SƠ BÁC SĨ (DOCTOR PROFILE)
        // ==========================================

        // GET: /Home/DoctorProfile
        [HttpGet]
        public async Task<IActionResult> DoctorProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            // Lấy hồ sơ Doctor kèm Specialty và User
            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // POST: /Home/DoctorProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoctorProfile(
            string FullName, string Email, string PhoneNumber, string Address,
            string Degree, decimal Price, string Bio,
            IFormFile AvatarFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            if (doctor == null) return NotFound();

            // Cập nhật thông tin tài khoản (ApplicationUser)
            user.FullName = FullName;
            user.Email = Email;
            user.PhoneNumber = PhoneNumber;
            user.Address = Address;
            await _userManager.UpdateAsync(user);

            // Cập nhật thông tin hành nghề (Doctor)
            doctor.Degree = Degree;
            doctor.Price = Price;
            doctor.Bio = Bio;

            // Xử lý upload ảnh đại diện nếu có chọn file mới
            if (AvatarFile != null && AvatarFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "doctors");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(AvatarFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await AvatarFile.CopyToAsync(fileStream);
                }

                // Xóa ảnh cũ nếu tồn tại
                if (!string.IsNullOrEmpty(doctor.Image))
                {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, "img", "doctors", doctor.Image);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                doctor.Image = uniqueFileName;
            }

            _context.Update(doctor);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction(nameof(DoctorProfile));
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
        // CỔNG THÔNG TIN BỆNH NHÂN (PORTAL)
        // ==========================================

        // 1. Xem danh sách Bệnh án của tôi
        public async Task<IActionResult> MyMedicalRecords()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            // Bảo mật: Chỉ lấy bệnh án có PatientId trùng với người đang đăng nhập
            var records = await _context.MedicalRecords
                .Include(m => m.Doctor)
                    .ThenInclude(d => d.User)
                .Where(m => m.Appointment.PatientId == userId) // <--- Rào chắn bảo mật quan trọng
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(records);
        }

        // GET: /Home/RecordDetails/5
        public async Task<IActionResult> RecordDetails(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            // BẢO MẬT: Chỉ lấy bệnh án đúng ID và bắt buộc thuộc về user đang đăng nhập
            var record = await _context.MedicalRecords
                .Include(m => m.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(m => m.Doctor)
                    .ThenInclude(d => d.User)
                // Lấy kèm đơn thuốc và chi tiết thuốc để hiển thị cho bệnh nhân xem
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

        // 2. Xem danh sách Đơn thuốc của tôi
        public async Task<IActionResult> MyPrescriptions()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            // Bảo mật: Lấy đơn thuốc thông qua Bệnh án của đúng bệnh nhân đó
            var prescriptions = await _context.Prescriptions
                .Include(p => p.MedicalRecord)
                    .ThenInclude(m => m.Doctor)
                        .ThenInclude(d => d.User)
                .Where(p => p.MedicalRecord.Appointment.PatientId == userId) // <--- Rào chắn bảo mật quan trọng
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            return View(prescriptions);
        }

        // 3. Xem danh sách Hóa đơn của tôi
        public async Task<IActionResult> MyInvoices()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            // Bảo mật: Lấy hóa đơn từ bệnh án của chính bệnh nhân
            var invoices = await _context.Invoices
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.Doctor)
                        .ThenInclude(d => d.User)
                .Where(i => i.MedicalRecord.Appointment.PatientId == userId) // <--- Rào chắn bảo mật quan trọng
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(invoices);
        }
    }
}
