using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;

namespace PBL3_HealthCare.Services
{
    public class InvoiceService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        public InvoiceService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hàm này sẽ chạy ngầm để đẻ ra Hóa đơn
        public async Task GenerateInvoiceAsync(int prescriptionId)
        {
            // 1. KÉO DATA: Lấy Đơn thuốc, Bệnh án, Lịch khám, Bác sĩ và Thuốc
            var prescription = await _context.Prescriptions
                .Include(p => p.MedicalRecord)
                    .ThenInclude(m => m.Appointment)
                        .ThenInclude(a => a.Doctor)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            if (prescription == null) return; // Nếu không có đơn thuốc thì dội ngược

            var appointment = prescription.MedicalRecord.Appointment;
            var doctor = appointment.Doctor;

            // 2. TÍNH TIỀN: Tiền khám + Tiền thuốc
            decimal consultationFee = doctor.Price; // Lấy giá khám của bác sĩ
            decimal medicinesFee = prescription.Details.Sum(d => d.Quantity * d.UnitPrice);
            decimal totalAmount = consultationFee + medicinesFee;

            // 3. TẠO HÓA ĐƠN CHA (Nối với Lịch khám)
            var invoice = new Invoice
            {
                AppointmentId = appointment.Id,
                TotalAmount = totalAmount,
                Status = InvoiceStatus.Unpaid, // Trạng thái chưa thanh toán
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(); // Lưu để EF Core đẻ ra cái invoice.Id

            // 4. TẠO CHI TIẾT 1: PHÍ KHÁM BỆNH (DỊCH VỤ)
            var feeDetail = new InvoiceDetail
            {
                InvoiceId = invoice.Id,
                Content = "Phí khám bệnh",   // Đã đổi từ ItemName sang Content
                Quantity = 1,
                UnitPrice = consultationFee,
                Type = (InvoiceDetailType)0  // 0: Dịch vụ (Ép kiểu theo đúng Enum sếp note)
            };
            _context.InvoiceDetails.Add(feeDetail);

            // 5. TẠO CHI TIẾT 2: QUÉT MẢNG THUỐC (THUỐC)
            foreach (var item in prescription.Details)
            {
                var medDetail = new InvoiceDetail
                {
                    InvoiceId = invoice.Id,
                    Content = "Thuốc: " + item.Medicine.Name, // Đã đổi từ ItemName sang Content
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Type = (InvoiceDetailType)1  // 1: Thuốc (Ép kiểu theo đúng Enum sếp note)
                };
                _context.InvoiceDetails.Add(medDetail);
            }

            // 6. CHỐT LƯU TẤT CẢ XUỐNG DATABASE
            await _context.SaveChangesAsync();
            // Nhắc bệnh nhân ra quầy đóng tiền (Dùng hàm của Quest 2)
            await _notificationService.CreateNotification(
                appointment.PatientId,
                $"Hệ thống vừa tạo một hóa đơn mới trị giá {totalAmount:N0} VNĐ. Vui lòng đến quầy thu ngân để thanh toán."
            );
        }
    }
}