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
        public InvoiceService(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // Hàm này sẽ chạy ngầm để đẻ ra Hóa đơn
        public async Task GenerateInvoiceAsync(int prescriptionId)
        {
            // 1. KÉO DATA: ÉP BUỘC CHỌC XUỐNG DB (AsNoTracking) ĐỂ TRÁNH LỖI RAM CACHE
            var prescription = await _context.Prescriptions
                .AsNoTracking() // 🪄 BÙA CHỐNG LỖI LÀ CHỮ NÀY ĐÂY SẾP!
                .Include(p => p.MedicalRecord)
                    .ThenInclude(m => m.Appointment)
                        .ThenInclude(a => a.Doctor)
                .Include(p => p.Details)
                    .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId);

            // 2. RÀ MÌN TỪNG BƯỚC (Để nếu lỗi nó báo rõ ràng, không báo Null giấu giếm nữa)
            if (prescription == null) throw new Exception("Hóa đơn: Không tìm thấy đơn thuốc!");
            if (prescription.MedicalRecord == null) throw new Exception("Hóa đơn: Đơn thuốc chưa có Bệnh án!");
            if (prescription.MedicalRecord.Appointment == null) throw new Exception("Hóa đơn: Bệnh án chưa có Lịch khám!");
            if (prescription.MedicalRecord.Appointment.Doctor == null) throw new Exception("Hóa đơn: Lịch khám chưa có thông tin Bác sĩ (Kiểm tra lại DB)!");

            var appointment = prescription.MedicalRecord.Appointment;
            var doctor = appointment.Doctor;

            // 3. TÍNH TIỀN: Tiền khám + Tiền thuốc
            decimal consultationFee = doctor.Price; // Lấy giá khám của bác sĩ
            decimal medicinesFee = prescription.Details != null ? prescription.Details.Sum(d => d.Quantity * d.UnitPrice) : 0;
            decimal totalAmount = consultationFee + medicinesFee;

            // 4. TẠO HÓA ĐƠN CHA (Nối với Lịch khám)
            var invoice = new Invoice
            {
                AppointmentId = appointment.Id,
                TotalAmount = totalAmount,
                MedicalRecordId = appointment.MedicalRecord.Id,
                Status = InvoiceStatus.Unpaid,
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(); // Lưu để đẻ ra cái invoice.Id

            // 5. TẠO CHI TIẾT 1: PHÍ KHÁM BỆNH
            var feeDetail = new InvoiceDetail
            {
                InvoiceId = invoice.Id,
                Content = "Phí khám bệnh",
                Quantity = 1,
                UnitPrice = consultationFee,
                Type = (InvoiceDetailType)0
            };
            _context.InvoiceDetails.Add(feeDetail);

            // 6. TẠO CHI TIẾT 2: QUÉT MẢNG THUỐC
            if (prescription.Details != null)
            {
                foreach (var item in prescription.Details)
                {
                    var medDetail = new InvoiceDetail
                    {
                        InvoiceId = invoice.Id,
                        Content = "Thuốc: " + item.Medicine?.Name,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Type = (InvoiceDetailType)1
                    };
                    _context.InvoiceDetails.Add(medDetail);
                }
            }

            // 7. CHỐT LƯU TẤT CẢ
            await _context.SaveChangesAsync();

            // Nhắc bệnh nhân ra quầy
            await _notificationService.CreateNotification(
                appointment.PatientId,
                $"Hệ thống vừa tạo một hóa đơn mới trị giá {totalAmount:N0} VNĐ. Vui lòng đến quầy thu ngân để thanh toán."
            );
        }
    }
}