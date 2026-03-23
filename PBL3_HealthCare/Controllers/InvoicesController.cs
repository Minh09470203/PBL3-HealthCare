using Microsoft.AspNetCore.Authorization;
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
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ========================================================
        // 1. HÀM INDEX: MÀN HÌNH DANH SÁCH HÓA ĐƠN CHO THU NGÂN
        // ========================================================
        public async Task<IActionResult> Index()
        {
            // Lấy toàn bộ Hóa đơn, móc qua Lịch khám để lấy tên Bệnh nhân và tên Bác sĩ
            var invoices = await _context.Invoices
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient) // Nhớ đảm bảo trong Appointment có public virtual ApplicationUser Patient
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User)
                .OrderByDescending(i => i.CreatedAt) // Hóa đơn mới nhất lên đầu
                .ToListAsync();

            return View(invoices);
        }

        // ========================================================
        // 2. HÀM CHI TIẾT: ĐỂ THU NGÂN ĐỌC CHO KHÁCH NGHE TỪNG MÓN
        // ========================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .Include(i => i.Details) // Lôi cái bảng InvoiceDetail ra
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        // ========================================================
        // 3. API POST: NÚT "XÁC NHẬN ĐÃ THU TIỀN" (CHỐT SỔ)
        // ========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }

            // Nếu lỡ tay bấm đúp hoặc đã thanh toán rồi thì chặn lại
            if (invoice.Status == InvoiceStatus.Paid) // Đảm bảo Enum của ông có chữ Paid (hoặc Completed)
            {
                TempData["Warning"] = "Hóa đơn này đã được thanh toán trước đó!";
                return RedirectToAction(nameof(Index));
            }

            // BÙM! Đổi trạng thái sang Đã Thu Tiền
            invoice.Status = InvoiceStatus.Paid;

            // LƯU Ý CHO SẾP: Trong bảng Invoice của ông chưa có cột PaymentDate (Ngày thanh toán).
            // Nếu muốn quản lý chặt (Biết khách trả tiền lúc mấy giờ), ông nên cấy thêm 
            // public DateTime? PaymentDate { get; set; } vào Model Invoice, rồi mở comment dòng dưới:
            // invoice.PaymentDate = DateTime.Now;

            _context.Update(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thu tiền thành công!";

            // Xong xuôi thì đá thu ngân về lại danh sách hóa đơn
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Print(int? id)
        {
            if (id == null) return NotFound();

            // Viết câu query "All-in-one" móc sạch sành sanh data liên quan
            var invoice = await _context.Invoices
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient) // Lấy tên Khách hàng
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User) // Lấy tên Bác sĩ
                .Include(i => i.Details) // Lấy danh sách các món tiền (Khám + Thuốc)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null) return NotFound();

            // Trả về một View đặc biệt (Thường View này sẽ không dùng Layout chung của hệ thống)
            return View(invoice);
        }

    }
}