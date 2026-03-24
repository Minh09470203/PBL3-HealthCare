using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;

namespace PBL3_HealthCare.Controllers
{
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            // 1. Lấy danh sách hóa đơn từ Database
            // 2. Dùng Include để nạp dữ liệu từ các bảng liên quan (Appointment, Patient)
            var invoices = _context.Invoices
                                   .Include(i => i.Appointment)
                                       .ThenInclude(a => a.Patient)
                                   .ToList();

            // 3. QUAN TRỌNG NHẤT: Truyền biến invoices vào View để hết lỗi Null
            return View(invoices);
        }
        [HttpPost]
        public IActionResult XacNhanThuTien(int id)
        {
            var invoice = _context.Invoices.Find(id);
            if (invoice == null) return Json(new { success = false, message = "Không tìm thấy hóa đơn!" });

            // Sửa thành InvoiceStatus.Paid cho đúng Model của Thái
            invoice.Status = InvoiceStatus.Paid;

            _context.SaveChanges();
            return Json(new { success = true, message = "Thanh toán thành công!" });
        }
    }
    }

