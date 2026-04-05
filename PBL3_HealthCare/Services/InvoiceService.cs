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
                $"Hệ thống vừa tạo một hóa đơn mới trị giá {totalAmount:N0} VNĐ. Vui lòng thanh toán."
            );
        }

        // Hàm mới: Đẻ hóa đơn ngay khi Admin duyệt Gói Khám
        public async Task GeneratePackageInvoiceAsync(int packageBookingId, int appointmentId)
        {
            // 1. Kéo data Gói Khám và Bệnh nhân
            var packageBooking = await _context.PackageBookings
                .Include(p => p.HealthPackage)
                .FirstOrDefaultAsync(p => p.Id == packageBookingId);

            if (packageBooking == null || packageBooking.HealthPackage == null)
                throw new Exception("Hóa đơn: Không tìm thấy thông tin Gói khám!");

            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null)
                throw new Exception("Hóa đơn: Không tìm thấy Lịch khám!");

            decimal packagePrice = packageBooking.HealthPackage.Price;

            // 2. Tạo Hóa Đơn Cha (Nối trực tiếp với Lịch khám, không cần MedicalRecord)
            var invoice = new Invoice
            {
                AppointmentId = appointment.Id,
                TotalAmount = packagePrice,
                Status = InvoiceStatus.Unpaid, // Trạng thái "Chưa thanh toán"
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(); // Lưu để đẻ ra cái invoice.Id

            // 3. Tạo Chi tiết Hóa đơn (Phí mua gói khám)
            var detail = new InvoiceDetail
            {
                InvoiceId = invoice.Id,
                Content = $"Gói khám: {packageBooking.HealthPackage.Name}",
                Quantity = 1,
                UnitPrice = packagePrice,
                Type = (InvoiceDetailType)0 // 0: Dịch vụ/Khám bệnh
            };

            _context.InvoiceDetails.Add(detail);
            await _context.SaveChangesAsync();

            // 4. Bắn thông báo Quả Chuông cho Bệnh nhân
            await _notificationService.CreateNotification(
                packageBooking.PatientId,
                $"Yêu cầu gói khám '{packageBooking.HealthPackage.Name}' của bạn đã được duyệt. Vui lòng thanh toán hóa đơn trị giá {packagePrice:N0} VNĐ."
            );
        }

        // Hàm mới: Đẻ hóa đơn cho Tiêm chủng
        public async Task GenerateVaccineInvoiceAsync(int vaccineBookingId, int appointmentId)
        {
            // 1. Kéo data lịch tiêm
            var booking = await _context.VaccinationBookings
                .Include(b => b.Vaccine)
                .FirstOrDefaultAsync(b => b.Id == vaccineBookingId);

            if (booking == null || booking.Vaccine == null)
                throw new Exception("Hóa đơn: Không tìm thấy thông tin Tiêm chủng!");

            decimal price = booking.Vaccine.Price;

            // 2. Tạo Hóa Đơn Cha (Tiêm chủng không có AppointmentId nên để trống hoặc nối nếu cần)
            var invoice = new Invoice
            {
                AppointmentId = appointmentId,
                TotalAmount = price,
                Status = InvoiceStatus.Unpaid,
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // 3. Tạo Chi tiết
            var detail = new InvoiceDetail
            {
                InvoiceId = invoice.Id,
                Content = $"Tiêm chủng Vaccine: {booking.Vaccine.Name}",
                Quantity = 1,
                UnitPrice = price,
                Type = (InvoiceDetailType)1 // 1: Thuốc/Vaccine
            };

            _context.InvoiceDetails.Add(detail);
            await _context.SaveChangesAsync();

            // 4. Thông báo
            await _notificationService.CreateNotification(
                booking.PatientId,
                $"Hóa đơn tiêm chủng '{booking.Vaccine.Name}' đã được khởi tạo. Số tiền: {price:N0} VNĐ."
            );
        }

        // Hàm mới: Đẻ hóa đơn cho Y Tế Tại Nhà
        public async Task GenerateHomeCareInvoiceAsync(int requestId, int appointmentId)
        {
            var request = await _context.HomeServiceRequests
                .Include(r => r.HomeService)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null || request.HomeService == null)
                throw new Exception("Hóa đơn: Không tìm thấy Yêu cầu Y tế tại nhà!");

            decimal price = request.HomeService.Price;

            var invoice = new Invoice
            {
                AppointmentId = appointmentId, // Tránh lỗi khóa ngoại
                TotalAmount = price,
                Status = InvoiceStatus.Unpaid,
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            var detail = new InvoiceDetail
            {
                InvoiceId = invoice.Id,
                Content = $"Dịch vụ tại nhà: {request.HomeService.Name}",
                Quantity = 1,
                UnitPrice = price,
                Type = (InvoiceDetailType)0 // Dịch vụ
            };

            _context.InvoiceDetails.Add(detail);
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                request.PatientId,
                $"Yêu cầu dịch vụ '{request.HomeService.Name}' tại nhà của bạn đã được duyệt. Vui lòng thanh toán hóa đơn trị giá {price:N0} VNĐ."
            );
        }
    }
}