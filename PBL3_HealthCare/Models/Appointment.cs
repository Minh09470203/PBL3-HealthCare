using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        // Ai đặt?
        public string PatientId { get; set; }
        [ForeignKey("PatientId")]
        public ApplicationUser? Patient { get; set; }

        // Đặt ai?
        public int DoctorId { get; set; }
        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        public DateTime Date { get; set; }
        public string? Reason { get; set; }

        // THÊM MỚI: Trạng thái (Lưu 0, 1, 2, 3...)
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
        public TimeSpan TimeSlot { get; set; } // Giờ khám (VD: 08:30)

        public string? Symptoms { get; set; } // Triệu chứng
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public virtual MedicalRecord? MedicalRecord { get; set; } // 1 cuộc hẹn có 1 kết quả khám
        public virtual Invoice? Invoice { get; set; }             // 1 cuộc hẹn có 1 hóa đơn
    }
 /* public enum AppointmentStatus
    {
        Pending,    // 0: Chờ duyệt
        Confirmed,  // 1: Đã duyệt
        Completed,  // 2: Hoàn thành
        Cancelled   // 3: Đã hủy
    } */
}