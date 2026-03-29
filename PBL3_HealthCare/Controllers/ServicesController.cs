using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data; // Đổi lại namespace theo project của sếp
using PBL3_HealthCare.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;


namespace PBL3_HealthCare.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Xem danh sách Gói Khám Sức Khỏe
        public async Task<IActionResult> Packages()
        {
            var packages = await _context.HealthPackages.ToListAsync();
            return View(packages);
        }

        [Authorize(Roles = "Patient")] // Phải đăng nhập mới được đặt
        public async Task<IActionResult> BookPackage(int id)
        {
            var package = await _context.HealthPackages.FindAsync(id);
            if (package == null) return NotFound();

            // Gửi thông tin gói khám ra View qua ViewBag để hiển thị cho đẹp
            ViewBag.PackageName = package.Name;
            ViewBag.PackagePrice = package.Price;

            var booking = new PackageBooking { HealthPackageId = id };
            return View(booking);
        }

        // [POST] Xử lý lưu Yêu cầu đặt gói
        [HttpPost]
        [Authorize(Roles = "Patient")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookPackage(PackageBooking model)
        {
            if (ModelState.IsValid)
            {
                // Lấy ID của bệnh nhân đang đăng nhập
                model.PatientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                model.Status = "Pending"; // Mặc định chờ Admin duyệt
                model.Id = 0;
                _context.PackageBookings.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Gửi yêu cầu thành công! Vui lòng chờ phòng khám liên hệ xếp Bác sĩ.";
                return RedirectToAction("MyHistory", "Home"); // Tạm thời đá về Trang chủ
            }
            var package = await _context.HealthPackages.FindAsync(model.HealthPackageId);
            if (package != null)
            {
                ViewBag.PackageName = package.Name;
                ViewBag.PackagePrice = package.Price;
            }
            return View(model);
        }

        // 2. Xem danh sách Vaccine (Chỉ lấy loại còn hàng)
        public async Task<IActionResult> Vaccines()
        {
            var vaccines = await _context.Vaccines
                                         .Where(v => v.StockQuantity > 0)
                                         .ToListAsync();
            return View(vaccines);
        }

        // [GET] Mở Form Đăng ký Tiêm
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> BookVaccine(int id)
        {
            var vaccine = await _context.Vaccines.FindAsync(id);
            if (vaccine == null || vaccine.StockQuantity <= 0)
            {
                TempData["Error"] = "Vaccine này hiện đã hết hàng!";
                return RedirectToAction("Vaccines");
            }

            // Truyền thông tin ra View để hiển thị
            ViewBag.VaccineName = vaccine.Name;
            ViewBag.Disease = vaccine.DiseasePrevented;
            ViewBag.Price = vaccine.Price;

            var booking = new VaccinationBooking { VaccineId = id };
            return View(booking);
        }

        // [POST] Xử lý lưu yêu cầu Tiêm
        [HttpPost]
        [Authorize(Roles = "Patient")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookVaccine(VaccinationBooking model)
        {
            // 1. Loại bỏ kiểm tra các trường tự sinh ngầm
            ModelState.Remove("PatientId");
            ModelState.Remove("Status");
            ModelState.Remove("Patient");
            ModelState.Remove("Vaccine");

            if (ModelState.IsValid)
            {
                model.PatientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                model.Status = "Pending";

                // 🔥 QUAN TRỌNG: Reset Id về 0 để Database tự tăng (tránh lỗi IDENTITY_INSERT)
                model.Id = 0;

                _context.VaccinationBookings.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đăng ký tiêm chủng thành công! Vui lòng đến đúng giờ.";
                return RedirectToAction("Index", "Home");
            }

            // 🔥 FIX LỖI "NÃO CÁ VÀNG": Nạp lại ViewBag nếu form bị lỗi nhập liệu
            var vaccine = await _context.Vaccines.FindAsync(model.VaccineId);
            if (vaccine != null)
            {
                ViewBag.VaccineName = vaccine.Name;
                ViewBag.Disease = vaccine.DiseasePrevented;
                ViewBag.Price = vaccine.Price;
            }

            return View(model);
        }

        // 3. Xem danh sách Dịch vụ Y tế tại nhà
        // 1. Xem danh sách Y tế tại nhà
        public async Task<IActionResult> HomeCare()
        {
            var services = await _context.HomeServices.ToListAsync();
            return View(services);
        }

        // 2. Mở Form Yêu cầu Y tế tại nhà
        [Authorize(Roles = "Patient")]
        [HttpGet]
        public async Task<IActionResult> BookHomeCare(int id)
        {
            var service = await _context.HomeServices.FindAsync(id);
            if (service == null) return NotFound();

            // 🔥 TRUYỀN TÊN BIẾN CHO VIEW
            ViewBag.ServiceName = service.Name;
            ViewBag.Price = service.Price;

            // Tự động mồi sẵn SĐT của User nếu có
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var patient = await _context.Users.FindAsync(userId);

            var request = new HomeServiceRequest
            {
                HomeServiceId = id,
                Phone = patient?.PhoneNumber // Tự load SĐT
            };
            return View(request);
        }

        // 3. Xử lý Form Yêu cầu
        [HttpPost]
        [Authorize(Roles = "Patient")]
        [ValidateAntiForgeryToken] // Nên có thẻ này bảo mật
        public async Task<IActionResult> BookHomeCare(HomeServiceRequest model)
        {
            // 🔥 BƯỚC 1: GỠ VALIDATION CHO CÁC TRƯỜNG DO BACKEND TỰ ĐIỀN
            ModelState.Remove("PatientId");
            ModelState.Remove("Status");
            ModelState.Remove("Patient");
            ModelState.Remove("HomeService");

            if (ModelState.IsValid)
            {
                model.PatientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                model.Status = "Pending";
                model.CreatedAt = DateTime.Now;

                model.Id = 0; // Chống lỗi IDENTITY_INSERT

                _context.HomeServiceRequests.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Gửi yêu cầu thành công! Lễ tân sẽ gọi điện xác nhận sớm nhất.";
                return RedirectToAction("Index", "Home");
            }

            // 🔥 BƯỚC 2: FIX LỖI "NÃO CÁ VÀNG" - NẠP LẠI VIEWBAG KHI FORM BỊ LỖI
            var service = await _context.HomeServices.FindAsync(model.HomeServiceId);
            if (service != null)
            {
                ViewBag.ServiceName = service.Name;
                ViewBag.Price = service.Price;
            }

            return View(model);
        }
    }
}