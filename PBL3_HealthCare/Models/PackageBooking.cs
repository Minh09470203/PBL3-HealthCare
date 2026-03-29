using System;
using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.Models
{
    public class PackageBooking
    {
        public int Id { get; set; }

        // Thêm dấu ? để báo cho hệ thống: "Cái này có thể tạm thời chưa có lúc nhận Form"
        public string? PatientId { get; set; }
        public ApplicationUser? Patient { get; set; }

        // Cái này bắt buộc phải có vì nó lấy từ <input type="hidden">
        public int HealthPackageId { get; set; }
        public HealthPackage? HealthPackage { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày khám")]
        [Display(Name = "Ngày giờ muốn khám")]
        public DateTime BookingDate { get; set; }

        // Status và CreatedAt nên gán giá trị mặc định để DB luôn có dữ liệu
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}