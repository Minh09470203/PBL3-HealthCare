using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR; // THÊM THƯ VIỆN SIGNALR
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Hubs; // THÊM THƯ VIỆN HUB
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
        
        // 🔥 1. KHAI BÁO THÊM 2 BỘ MÁY PHÁT SÓNG
        private readonly NotificationService _notificationService;
        private readonly IHubContext<NotificationHub> _hubContext;

        // 🔥 2. TIÊM VÀO CONSTRUCTOR
        public AdminController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager, 
            InvoiceService invoiceService,
            NotificationService notificationService,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _invoiceService = invoiceService;
            _notificationService = notificationService;
            _hubContext = hubContext;
        }

        // ========================================================
        // 1. HÀM INDEX: LOAD GIAO DIỆN & CÁC CON SỐ TỔNG QUAN
        // ========================================================
        public async Task<IActionResult> Index()
        {
            var activePatientIdsFromAppointments = await _context.Appointments.Select(a => a.PatientId).ToListAsync();
            var activePatientIdsFromPackages = await _context.PackageBookings.Select(p => p.PatientId).ToListAsync();
            var activePatientIdsFromVaccines = await _context.VaccinationBookings.Select(v => v.PatientId).ToListAsync();
            var activePatientIdsFromHomeCares = await _context.HomeServiceRequests.Select(h => h.PatientId).ToListAsync();

            var totalActivePatients = activePatientIdsFromAppointments
                .Concat(activePatientIdsFromPackages)
                .Concat(activePatientIdsFromVaccines)
                .Concat(activePatientIdsFromHomeCares)
                .Distinct()
                .Count();

            ViewBag.TotalPatients = totalActivePatients;
            ViewBag.TotalDoctors = await _context.Doctors.CountAsync();

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            ViewBag.MonthlyRevenue = await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Paid
                         && i.CreatedAt.Month == currentMonth
                         && i.CreatedAt.Year == currentYear)
                .SumAsync(i => i.TotalAmount);

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
                var rawAppointments = await _context.Appointments
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.Specialty)
                    .Where(a => a.Doctor != null && a.Doctor.Specialty != null)
                    .ToListAsync();

                var specialtyStats = rawAppointments
                    .GroupBy(a => a.Doctor.Specialty.Name)
                    .Select(g => new
                    {
                        SpecialtyName = g.Key,
                        PatientCount = g.Count()
                    })
                    .ToList<dynamic>(); 

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

                var sixMonthsAgo = DateTime.Now.AddMonths(-5);
                var startDate = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1);

                var rawInvoices = await _context.Invoices
                    .Where(i => i.Status == InvoiceStatus.Paid && i.CreatedAt >= startDate)
                    .ToListAsync();

                var revenueStats = rawInvoices
                    .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                    .Select(g => new
                    {
                        Month = g.Key.Month.ToString("00") + "/" + g.Key.Year, 
                        Total = g.Sum(i => i.TotalAmount)
                    })
                    .OrderBy(r => r.Month)
                    .ToList<dynamic>();

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

                return Json(new
                {
                    PieChartData = specialtyStats,
                    BarChartData = revenueStats
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("======= LỖI GET CHART DATA =======");
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Lỗi máy chủ nội bộ: " + ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ApproveAndAssignDoctor(int id)
        {
            var request = await _context.PackageBookings
                .Include(p => p.Patient)
                .Include(p => p.HealthPackage)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (request == null)
            {
                TempData["Error"] = "Không tìm thấy yêu cầu này!";
                return RedirectToAction("Index");
            }

            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialty)
                .ToListAsync();

            ViewBag.Doctors = doctors;

            return View(request);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAndAssignDoctor(int packageBookingId, int doctorId)
        {
            var request = await _context.PackageBookings
                .Include(p => p.HealthPackage)
                .FirstOrDefaultAsync(p => p.Id == packageBookingId);

            if (request != null)
            {
                var appointment = new Appointment
                {
                    PatientId = request.PatientId,
                    DoctorId = doctorId,
                    Date = request.BookingDate.Date, 
                    TimeSlot = request.BookingDate.TimeOfDay, 
                    Reason = $"[Gói Khám] {request.HealthPackage.Name}",
                    Status = AppointmentStatus.Confirmed,
                    CreatedAt = DateTime.Now
                };

                request.Status = "Approved";

                _context.Appointments.Add(appointment);
                _context.Update(request);
                await _context.SaveChangesAsync();

                await _invoiceService.GeneratePackageInvoiceAsync(request.Id, appointment.Id);

                // 🔥 BẮN THÔNG BÁO CHO BỆNH NHÂN (Xác nhận lịch)
                string msgPatient = $"Lịch khám gói '{request.HealthPackage.Name}' của bạn đã được xác nhận vào lúc {appointment.TimeSlot:hh\\:mm} ngày {appointment.Date:dd/MM/yyyy}.";
                await _notificationService.CreateNotification(request.PatientId, msgPatient);
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", request.PatientId, msgPatient);

                // 🔥 BẮN THÔNG BÁO CHO BÁC SĨ (Có ca mới)
                var doctorInfo = await _context.Doctors.FindAsync(doctorId);
                if (doctorInfo != null)
                {
                    string msgDoctor = $"Admin vừa phân công cho bạn một lịch khám gói '{request.HealthPackage.Name}' vào lúc {appointment.TimeSlot:hh\\:mm} ngày {appointment.Date:dd/MM/yyyy}.";
                    await _notificationService.CreateNotification(doctorInfo.UserId, msgDoctor);
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", doctorInfo.UserId, msgDoctor);
                }

                TempData["Success"] = "Đã phân công Bác sĩ và tạo Lịch khám thành công!";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CompleteVaccination(int bookingId)
        {
            var booking = await _context.VaccinationBookings
                .Include(b => b.Vaccine)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking != null && booking.Status == "Approved")
            {
                if (booking.Vaccine.StockQuantity <= 0)
                {
                    TempData["Error"] = "Lỗi: Vaccine này đã hết trong kho!";
                    return RedirectToAction("PendingVaccinations"); 
                }

                booking.Status = "Completed";
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
            return RedirectToAction("PendingVaccinations"); 
        }

        [HttpGet]
        public async Task<IActionResult> PendingPackages()
        {
            var now = DateTime.Now; 

            var pendingList = await _context.PackageBookings
                .Include(p => p.Patient)
                .Include(p => p.HealthPackage)
                .Where(p => p.Status == "Pending" && p.BookingDate >= now)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return View(pendingList);
        }

        [HttpGet]
        public async Task<IActionResult> PendingVaccinations()
        {
            var now = DateTime.Now;

            var list = await _context.VaccinationBookings
                .Include(v => v.Patient)
                .Include(v => v.Vaccine)
                .Where(v => (v.Status == "Pending" || v.Status == "Approved") && v.BookingDate >= now)
                .OrderBy(v => v.BookingDate)
                .ToListAsync();

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveVaccine(int bookingId)
        {
            var booking = await _context.VaccinationBookings
                .Include(v => v.Vaccine)
                .FirstOrDefaultAsync(v => v.Id == bookingId);

            if (booking != null)
            {
                var appointment = new Appointment
                {
                    PatientId = booking.PatientId,
                    DoctorId = 1, 
                    Date = booking.BookingDate.Date,
                    TimeSlot = booking.BookingDate.TimeOfDay,
                    Reason = $"[Tiêm Chủng] {booking.Vaccine?.Name}",
                    Status = AppointmentStatus.Confirmed, 
                    CreatedAt = DateTime.Now
                };

                booking.Status = "Approved"; 

                _context.Appointments.Add(appointment);
                _context.Update(booking);
                await _context.SaveChangesAsync();

                await _invoiceService.GenerateVaccineInvoiceAsync(booking.Id, appointment.Id);

                // 🔥 BẮN THÔNG BÁO CHO BỆNH NHÂN
                string msgPatient = $"Lịch tiêm chủng '{booking.Vaccine.Name}' của bạn đã được chốt vào lúc {appointment.TimeSlot:hh\\:mm} ngày {appointment.Date:dd/MM/yyyy}.";
                await _notificationService.CreateNotification(booking.PatientId, msgPatient);
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", booking.PatientId, msgPatient);

                TempData["Success"] = "Đã duyệt lịch và tạo hóa đơn!";
            }
            return RedirectToAction("PendingVaccinations");
        }

        [HttpGet]
        public async Task<IActionResult> PendingHomeCare()
        {
            var now = DateTime.Now;

            var list = await _context.HomeServiceRequests
                .Include(h => h.Patient)
                .Include(h => h.HomeService)
                .Where(h => h.Status == "Pending" && h.RequestDate >= now)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            return View(list);
        }

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

        [HttpPost]
        public async Task<IActionResult> ApproveHomeCare(int requestId, int doctorId)
        {
            var request = await _context.HomeServiceRequests
                .Include(h => h.HomeService)
                .FirstOrDefaultAsync(h => h.Id == requestId);

            if (request != null)
            {
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
                await _context.SaveChangesAsync(); 

                await _invoiceService.GenerateHomeCareInvoiceAsync(request.Id, appointment.Id);

                // 🔥 BẮN THÔNG BÁO CHO BỆNH NHÂN (Xác nhận lịch)
                string msgPatient = $"Yêu cầu dịch vụ '{request.HomeService.Name}' tại nhà của bạn đã được xác nhận. Bác sĩ sẽ đến vào lúc {appointment.TimeSlot:hh\\:mm} ngày {appointment.Date:dd/MM/yyyy}.";
                await _notificationService.CreateNotification(request.PatientId, msgPatient);
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", request.PatientId, msgPatient);

                // 🔥 BẮN THÔNG BÁO CHO BÁC SĨ ĐƯỢC PHÂN CÔNG
                var doctorInfo = await _context.Doctors.FindAsync(doctorId);
                if (doctorInfo != null)
                {
                    string msgDoctor = $"Admin vừa phân công cho bạn ca Y tế tại nhà '{request.HomeService.Name}' vào lúc {appointment.TimeSlot:hh\\:mm} ngày {appointment.Date:dd/MM/yyyy}. Địa chỉ: {request.Address}";
                    await _notificationService.CreateNotification(doctorInfo.UserId, msgDoctor);
                    await _hubContext.Clients.All.SendAsync("ReceiveNotification", doctorInfo.UserId, msgDoctor);
                }

                TempData["Success"] = "Đã phân công nhân sự, tạo Lịch hẹn và Hóa đơn thành công!";
            }
            return RedirectToAction("PendingHomeCare");
        }
    }
}