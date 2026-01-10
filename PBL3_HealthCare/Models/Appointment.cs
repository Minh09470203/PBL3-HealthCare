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
        public TimeSpan TimeSlot { get; set; } // Giờ khám (VD: 08:30)

        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled
        public string? Symptoms { get; set; } // Triệu chứng
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}