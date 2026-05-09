using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Admin, Doctor")]
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
                    .ThenInclude(a => a.Patient)
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
            if (invoice.Status == InvoiceStatus.Paid)
            {
                TempData["Warning"] = "Hóa đơn này đã được thanh toán trước đó!";
                return RedirectToAction(nameof(Index));
            }

            // Đổi trạng thái sang Đã Thu Tiền
            invoice.Status = InvoiceStatus.Paid;

            _context.Update(invoice);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thu tiền thành công!";

            // Xong xuôi thì đá về lại danh sách hóa đơn
            return RedirectToAction(nameof(Index));
        }

        // ========================================================
        // 4. HÀM IN HÓA ĐƠN
        // ========================================================
        [HttpGet]
        public async Task<IActionResult> Print(int? id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Patient)
                .Include(i => i.Appointment)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User)
                .Include(i => i.Details)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }
    }
}