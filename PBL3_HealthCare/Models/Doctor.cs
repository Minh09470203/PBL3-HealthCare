using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Doctor
    {
        [Key]
        public int Id { get; set; }

        // Liên kết 1-1 với tài khoản User (để đăng nhập)
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        // Liên kết n-1 với Chuyên khoa
        public int SpecialtyId { get; set; }
        [ForeignKey("SpecialtyId")]
        public Specialty? Specialty { get; set; }
        public string? Bio { get; set; } // Giới thiệu
        public string? Degree { get; set; } // Học vị (Thạc sĩ, Bác sĩ CKI...)
        public decimal Price { get; set; } // Giá khám
        public string? Image { get; set; } // Link ảnh
        public bool IsVideoAvailable { get; set; } = false;

        // Mối quan hệ: Bác sĩ có nhiều lịch hẹn
        public virtual ICollection<Appointment>? Appointments { get; set; }
        // Mối quan hệ: Bác sĩ có nhiều lịch trực
        public ICollection<Schedule>? Schedules { get; set; }
    }
}