namespace PBL3_HealthCare.Models
{
    // Trạng thái đơn thuốc
    public enum PrescriptionStatus
    {
        New = 0,        // Mới kê (Chưa mua)
        Sold = 1,       // Đã thanh toán/Đã lấy thuốc
        Cancelled = 2   // Đã hủy
    }
    public enum AppointmentStatus
    {
        Pending,    // 0: Chờ duyệt
        Confirmed,  // 1: Đã duyệt
        Completed,  // 2: Hoàn thành
        Cancelled   // 3: Đã hủy
    }

    // Trạng thái hóa đơn
    public enum InvoiceStatus
    {
        Unpaid = 0,     // Chưa thanh toán
        Paid = 1,       // Đã thanh toán
        Cancelled = 2   // Đã hủy
    }

    // Loại chi dòng tiền (Để biết đâu là tiền khám, đâu là tiền thuốc)
    public enum InvoiceDetailType
    {
        Service = 0,    // Dịch vụ (Khám, Xét nghiệm...)
        Medicine = 1    // Thuốc
    }
}