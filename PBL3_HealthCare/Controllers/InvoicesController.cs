using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    public class InvoicesController : Controller 
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public InvoicesController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // POST: api/Invoices/Generate/5
        // Endpoint này dùng để tự động sinh hóa đơn khi Bệnh án + Đơn thuốc đã chốt
        [HttpPost("Generate/{medicalRecordId}")]
        public async Task<ActionResult<Invoice>> GenerateInvoice(int medicalRecordId)
        {
            // Kiểm tra bệnh án có tồn tại không và lấy dữ liệu liên quan
            // Lưu ý: Phải Include Doctor để lấy Price và PrescriptionDetails để tính tiền thuốc
            var medicalRecord = await _context.MedicalRecords
                .Include(m => m.Doctor)
                .Include(m => m.Prescriptions)
                    .ThenInclude(p => p.PrescriptionDetails)
                        .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(m => m.Id == medicalRecordId);

            if (medicalRecord == null)
            {
                return NotFound(new { message = "Không tìm thấy bệnh án để tạo hóa đơn." });
            }

            // Kiểm tra nếu hóa đơn cho bệnh án này đã tồn tại rồi thì không tạo trùng
            var existingInvoice = await _context.Invoices
                .AnyAsync(i => i.MedicalRecordId == medicalRecordId);
            if (existingInvoice)
            {
                return BadRequest(new { message = "Hóa đơn cho bệnh án này đã được khởi tạo trước đó." });
            }

            try
            {
                //Tính toán theo công thức: Tổng = Giá khám + Tổng(Giá thuốc * Số lượng)
                decimal doctorFee = medicalRecord.Doctor?.Price ?? 0;

                decimal medicineTotal = 0;
                if (medicalRecord.Prescriptions != null)
                {
                    medicineTotal = medicalRecord.Prescriptions
                        .SelectMany(p => p.PrescriptionDetails)
                        .Sum(detail => detail.Quantity * (detail.Medicine?.Price ?? 0));
                }

                decimal totalAmount = doctorFee + medicineTotal;

                //Khởi tạo đối tượng Invoice mới
                var invoice = new Invoice
                {
                    MedicalRecordId = medicalRecordId,
                    TotalAmount = totalAmount,
                    CreatedAt = DateTime.Now,
                    Status = 0, // Mặc định là Pending theo yêu cầu
                    Note = $"Auto-generated from Medical Record #{medicalRecordId}"
                };
                
                //Lưu vào database
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
                var patientId = medicalRecord.Appointment?.PatientId;
                if (!string.IsNullOrEmpty(patientId))
                {
                    await _notificationService.CreateNotification(patientId, "Bạn có hóa đơn mới cần thanh toán. Vui lòng kiểm tra mục hóa đơn.");
                }
                return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi sinh hóa đơn tự động", error = ex.Message });
            }
        }

        // GET: api/Invoices/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.ApplicationUser) // Lấy tên, địa chỉ bệnh nhân
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.Doctor)  // Lấy tên bác sĩ, chuyên khoa
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.Prescriptions)
                        .ThenInclude(p => p.PrescriptionDetails)
                            .ThenInclude(d => d.Medicine) // Lấy danh sách tên thuốc và đơn giá
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return NotFound(new { message = "Không tìm thấy hóa đơn" });

            return Ok(invoice);
        }
        // POST: Invoices/ConfirmPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken] // Thêm bảo mật cho Form phía Server-side
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            // Tìm hóa đơn trong database
            var invoice = await _context.Invoices.FindAsync(id);

            if (invoice == null)
            {
                return NotFound();
            }

            
            invoice.Status = InvoiceStatus.Paid; // Đổi trạng thái thành Paid 
            invoice.CreatedAt = DateTime.Now;  // Lưu thời điểm thanh toán thực tế

            try
            {
                _context.Update(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Xác nhận thanh toán thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
            }

            // Sau khi xác nhận xong, quay lại trang danh sách hóa đơn hoặc chi tiết
            return RedirectToAction(nameof(GetInvoice), new { id = invoice.Id });
        }
        // GET: Invoices/Print/5
        public async Task<IActionResult> Print(int id)
        {
            // Câu query .Include() để gom thông tin phục vụ in ấn
            var invoice = await _context.Invoices
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.ApplicationUser) // Thông tin Bệnh nhân
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.Doctor)          // Thông tin Bác sĩ
                        .ThenInclude(d => d.User)
                .Include(i => i.MedicalRecord)
                    .ThenInclude(m => m.Prescriptions)
                        .ThenInclude(p => p.PrescriptionDetails)
                            .ThenInclude(d => d.Medicine) // Chi tiết thuốc trong hóa đơn
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }
            // Trả về một View (Print.cshtml)
            return View(invoice);
        }
    }
}
