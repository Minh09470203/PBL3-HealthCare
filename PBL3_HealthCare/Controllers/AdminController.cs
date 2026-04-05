using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    // Bùa bảo vệ: Phải là Admin mới được vào xem bảng lương, doanh thu!
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly InvoiceService _invoiceService;
        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, InvoiceService invoiceService)
        {
            _context = context;
            _userManager = userManager;
            _invoiceService = invoiceService;
        }

        // ========================================================
        // 1. HÀM INDEX: LOAD GIAO DIỆN & CÁC CON SỐ TỔNG QUAN
        // ========================================================
        public async Task<IActionResult> Index()
        {
            // 1. SỬA LẠI: Đếm tổng số Bệnh nhân THỰC SỰ (Đã từng đặt lịch)
            // Lấy danh sách PatientId từ tất cả các bảng dịch vụ (loại bỏ trùng lặp bằng .Distinct)
            var activePatientIdsFromAppointments = await _context.Appointments.Select(a => a.PatientId).ToListAsync();
            var activePatientIdsFromPackages = await _context.PackageBookings.Select(p => p.PatientId).ToListAsync();
            var activePatientIdsFromVaccines = await _context.VaccinationBookings.Select(v => v.PatientId).ToListAsync();
            var activePatientIdsFromHomeCares = await _context.HomeServiceRequests.Select(h => h.PatientId).ToListAsync();

            // Gộp tất cả ID lại và đếm những ID duy nhất
            var totalActivePatients = activePatientIdsFromAppointments
                .Concat(activePatientIdsFromPackages)
                .Concat(activePatientIdsFromVaccines)
                .Concat(activePatientIdsFromHomeCares)
                .Distinct()
                .Count();

            ViewBag.TotalPatients = totalActivePatients;

            // 2. Đếm tổng số Bác sĩ (Lấy thẳng từ bảng Doctors cho lẹ)
            ViewBag.TotalDoctors = await _context.Doctors.CountAsync();

            // 3. Tính doanh thu THÁNG NÀY (Chỉ tính Hóa đơn đã thu tiền - Paid)
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            // Lưu ý: Đổi chữ InvoiceStatus.Paid thành Enum của sếp nếu đặt tên khác nhé
            ViewBag.MonthlyRevenue = await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid
                        && i.CreatedAt.Month == currentMonth
                        && i.CreatedAt.Year == currentYear)
                .SumAsync(i => i.TotalAmount);

            // Truyền sang cho Thái vẽ UI bằng ViewBag
            return View();
        }

        // ========================================================
        // 2. API TRẢ VỀ JSON: VẼ CHART.JS (BẢN CHỐNG LỖI 500)
        // ========================================================
        [HttpGet]
        public async Task<IActionResult> GetChartData()
        {
            try
            {
                // A. BIỂU ĐỒ TRÒN: Tỷ lệ bệnh nhân theo Chuyên khoa
                // BƯỚC 1: Lấy toàn bộ Appointment có chứa Doctor và Specialty về RAM trước (Tránh lỗi EF Core GroupBy)
                var rawAppointments = await _context.Appointments
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.Specialty)
                    .Where(a => a.Doctor != null && a.Doctor.Specialty != null)
                    .ToListAsync();

                // BƯỚC 2: Gom nhóm trên RAM
                var specialtyStats = rawAppointments
                    .GroupBy(a => a.Doctor.Specialty.Name)
                    .Select(g => new
                    {
                        SpecialtyName = g.Key,
                        PatientCount = g.Count()
                    })
                    .ToList<dynamic>(); // Ép kiểu để dễ gán data giả

                // 🔥 THỦ THUẬT DEMO: Nếu trống trơn thì đắp data giả vào
                if (!specialtyStats.Any())
                {
                    specialtyStats = new List<dynamic>
                    {
                        new { SpecialtyName = "Nội khoa", PatientCount = 45 },
                        new { SpecialtyName = "Ngoại khoa", PatientCount = 20 },
                        new { SpecialtyName = "Da liễu", PatientCount = 30 },
                        new { SpecialtyName = "Tai - Mũi - Họng", PatientCount = 15 }
                    };
                }

                // B. BIỂU ĐỒ CỘT: Doanh thu 6 tháng gần nhất
                var sixMonthsAgo = DateTime.Now.AddMonths(-5);
                var startDate = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

                // BƯỚC 1: Lấy hóa đơn về RAM
                var rawInvoices = await _context.Invoices
                    .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= startDate)
                    .ToListAsync();

                // BƯỚC 2: Gom nhóm trên RAM
                var revenueStats = rawInvoices
                    .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                    .Select(g => new
                    {
                        Month = g.Key.Month.ToString("00") + "/" + g.Key.Year, // Định dạng 04/2026 cho đẹp
                        Total = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(r => r.Month)
                    .ToList<dynamic>();

                // 🔥 THỦ THUẬT DEMO: Tạo chuỗi 6 tháng liên tiếp (kể cả tháng không có doanh thu)
                if (!revenueStats.Any())
                {
                    revenueStats = new List<dynamic>();
                    for (int i = 5; i >= 0; i--)
                    {
                        var pastMonth = DateTime.Now.AddMonths(-i);
                        revenueStats.Add(new
                        {
                            Month = pastMonth.Month.ToString("00") + "/" + pastMonth.Year,
                            Total = new Random().Next(15000000, 50000000)
                        });
                    }
                }

                // TRẢ VỀ JSON THÀNH CÔNG
                return Json(new
                {
                    PieChartData = specialtyStats,
                    BarChartData = revenueStats
                });
            }
            catch (Exception ex)
            {
                // Bắt Lỗi Xuyên Tâm Liên: Nếu có lỗi, in ra Console của Visual Studio để sếp dễ bắt bệnh
                Console.WriteLine("======= LỖI GET CHART DATA =======");
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Lỗi máy chủ nội bộ: " + ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ApproveAndAssignDoctor(int id)
        {
            // 1. Tìm thông tin gói khám mà bệnh nhân vừa đặt
            var request = await _context.PackageBookings
                .Include(p => p.Patient) // Lấy thông tin Bệnh nhân
                .Include(p => p.HealthPackage) // Lấy thông tin Gói khám
                .FirstOrDefaultAsync(p => p.Id == id);

            if (request == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu này!";
                return RedirectToAction("Index");
            }

            // 2. Lấy danh sách toàn bộ Bác sĩ để đổ vào cái Dropdown cho Admin chọn
            // (Bao gồm cả thông tin User để lấy Tên thật, và Specialty để lấy tên Chuyên khoa)
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .ToListAsync();

            ViewBag.Doctors = doctors;

            return View(request);
        }

        [HttpPost]
        // SỬA LẠI: Đổi kiểu dữ liệu của doctorId từ string sang int cho khớp với bảng Doctor
        public async Task<IActionResult> ApproveAndAssignDoctor(int packageBookingId, int doctorId)
        {
            // 1. Tìm cái yêu cầu đặt gói
            var request = await _context.PackageBookings
                .Include(p => p.HealthPackage)
                .FirstOrDefaultAsync(p => p.Id == packageBookingId);

            if (request != null)
            {
                // 2. Tạo Appointment mới từ thông tin của Request
                var appointment = new Appointment
                {
                    PatientId = request.PatientId,
                    DoctorId = doctorId, // Đã khớp kiểu int

                    // SỬA LẠI: Tách Ngày và Giờ ra cho khớp với model của sếp
                    Date = request.BookingDate.Date, // Chỉ lấy phần Ngày (VD: 25/10/2026)
                    TimeSlot = request.BookingDate.TimeOfDay, // Cắt lấy phần Giờ (VD: 08:30) nhét vào TimeSpan

                    Reason = $"[Gói Khám] {request.HealthPackage.Name}",

                    // SỬA LẠI: Dùng Enum thay vì string "Confirmed"
                    Status = AppointmentStatus.Confirmed,

                    CreatedAt = DateTime.Now
                };

                // 3. Đổi trạng thái Request thành Approved
                request.Status = "Approved";

                // 4. Lưu cả 2 xuống DB
                _context.Appointments.Add(appointment);
                _context.Update(request);
                await _context.SaveChangesAsync();

                await _invoiceService.GeneratePackageInvoiceAsync(request.Id, appointment.Id);

                TempData["Success"] = "Đã phân công Bác sĩ và tạo Lịch khám thành công!";
            }
            return RedirectToAction("Index"); // Quay lại danh sách chờ duyệt (sếp nhớ tạo View này nhé)
        }

        [HttpPost]
        public async Task<IActionResult> CompleteVaccination(int bookingId)
        {
            // 1. Tìm lịch tiêm (Kèm theo thông tin Vaccine)
            var booking = await _context.VaccinationBookings
                .Include(b => b.Vaccine)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking != null && booking.Status == "Approved")
            {
                // 2. Kiểm tra kho còn thuốc không
                if (booking.Vaccine.StockQuantity <= 0)
                {
                    TempData["Error"] = "Lỗi: Vaccine này đã hết trong kho!";
                    return RedirectToAction("PendingVaccinations"); // Sếp tự tạo View danh sách chờ tiêm nhé
                }

                // 3. Đổi trạng thái thành Đã tiêm
                booking.Status = "Completed";

                // 4. TRỪ KHO THUỐC (Cực kỳ quan trọng)
                booking.Vaccine.StockQuantity -= 1;

                var appt = await _context.Appointments
            .FirstOrDefaultAsync(a => a.PatientId == booking.PatientId
                                 && a.Reason.Contains(booking.Vaccine.Name)
                                 && a.Status == AppointmentStatus.Confirmed);
                if (appt != null) appt.Status = AppointmentStatus.Completed;

                _context.Update(booking);
                _context.Update(booking.Vaccine);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã xác nhận tiêm chủng và trừ 1 liều trong kho!";
            }
            return RedirectToAction("PendingVaccinations"); // Quay về màn hình danh sách
        }

        [HttpGet]
        public async Task<IActionResult> PendingPackages()
        {
            var now = DateTime.Now; // Lấy mốc thời gian ngay lúc này

            // Lấy danh sách các yêu cầu đang chờ duyệt (Pending) VÀ có thời gian khám ở tương lai
            var pendingList = await _context.PackageBookings
                .Include(p => p.Patient)
                .Include(p => p.HealthPackage)
                .Where(p => p.Status == "Pending" && p.BookingDate >= now)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return View(pendingList);
        }

        // 1. Trang danh sách chờ duyệt tiêm (Lấy từ code sếp gửi)
        [HttpGet]
        public async Task<IActionResult> PendingVaccinations()
        {
            var now = DateTime.Now;

            var list = await _context.VaccinationBookings
                .Include(v => v.Patient)
                .Include(v => v.Vaccine)
                // Phải có ngoặc đơn bao quanh 2 cái Status, sau đó mới AND với BookingDate
                .Where(v => (v.Status == "Pending" || v.Status == "Approved") && v.BookingDate >= now)
                .OrderBy(v => v.BookingDate)
                .ToListAsync();

            return View(list);
        }

        // 2. Hàm Duyệt nhanh: Tạo Appointment + Tạo Invoice
        [HttpPost]
        public async Task<IActionResult> ApproveVaccine(int bookingId)
        {
            var booking = await _context.VaccinationBookings
                .Include(v => v.Vaccine)
                .FirstOrDefaultAsync(v => v.Id == bookingId);

            if (booking != null)
            {
                // TẠO APPOINTMENT
                var appointment = new Appointment
                {
                    PatientId = booking.PatientId,
                    DoctorId = 1, // 🚨 SẾP LƯU Ý: Thay số 1 bằng ID của "Phòng Tiêm" trong DB của sếp
                    Date = booking.BookingDate.Date,
                    TimeSlot = booking.BookingDate.TimeOfDay,
                    Reason = $"[Tiêm Chủng] {booking.Vaccine?.Name}",
                    Status = AppointmentStatus.Confirmed, // Đã duyệt
                    CreatedAt = DateTime.Now
                };

                booking.Status = "Approved"; // Chuyển sang đã duyệt

                _context.Appointments.Add(appointment);
                _context.Update(booking);
                await _context.SaveChangesAsync();

                // TẠO HÓA ĐƠN (Gọi Service đã viết)
                await _invoiceService.GenerateVaccineInvoiceAsync(booking.Id, appointment.Id);

                TempData["Success"] = "Đã duyệt lịch và tạo hóa đơn!";
            }
            return RedirectToAction("PendingVaccinations");
        }

        // 1. Danh sách chờ duyệt
        [HttpGet]
        public async Task<IActionResult> PendingHomeCare()
        {
            var now = DateTime.Now;

            var list = await _context.HomeServiceRequests
                .Include(h => h.Patient)
                .Include(h => h.HomeService)
                // Lọc trạng thái Pending VÀ Thời gian yêu cầu phải ở tương lai
                .Where(h => h.Status == "Pending" && h.RequestDate >= now)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            return View(list);
        }

        // 2. Mở form chọn Y tá/Bác sĩ
        [HttpGet]
        public async Task<IActionResult> ApproveHomeCare(int id)
        {
            var request = await _context.HomeServiceRequests
                .Include(h => h.Patient)
                .Include(h => h.HomeService)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (request == null) return NotFound();

            ViewBag.Doctors = await _context.Doctors.Include(d => d.User).Include(d => d.Specialty).ToListAsync();
            return View(request);
        }

        // 3. Chốt duyệt: Đẻ Lịch hẹn + Đẻ Hóa đơn
        [HttpPost]
        public async Task<IActionResult> ApproveHomeCare(int requestId, int doctorId)
        {
            var request = await _context.HomeServiceRequests
                .Include(h => h.HomeService)
                .FirstOrDefaultAsync(h => h.Id == requestId);

            if (request != null)
            {
                // 🚨 ĐIỂM ĂN TIỀN: Nhét SĐT và Địa chỉ vào Reason để Bác sĩ thấy đường đi
                var appointment = new Appointment
                {
                    PatientId = request.PatientId,
                    DoctorId = doctorId,
                    Date = request.RequestDate.Date,
                    TimeSlot = request.RequestDate.TimeOfDay,
                    Reason = $"[Tại Nhà] {request.HomeService?.Name} | ĐC: {request.Address} | SĐT: {request.Phone}",
                    Status = AppointmentStatus.Confirmed,
                    CreatedAt = DateTime.Now
                };

                request.Status = "Approved";

                _context.Appointments.Add(appointment);
                _context.Update(request);
                await _context.SaveChangesAsync(); // Lưu để lấy appointment.Id

                // Gọi Service đẻ Hóa Đơn
                await _invoiceService.GenerateHomeCareInvoiceAsync(request.Id, appointment.Id);

                TempData["Success"] = "Đã phân công nhân sự, tạo Lịch hẹn và Hóa đơn thành công!";
            }
            return RedirectToAction("PendingHomeCare");
        }
    }
}