using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace PBL3_HealthCare.Models 
{
    public class Specialty
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "Tên chuyên khoa không được để trống!")]
        public string Name { get; set; } // Tên khoa: Tim mạch, Nha khoa...
        public string? Description { get; set; } // Mô tả
        public string? Image { get; set; } // Ảnh đại diện

        // Mối quan hệ: Một khoa có nhiều bác sĩ
        public ICollection<Doctor>? Doctors { get; set; }
    }
}