using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class MedicalRecord
    {
        [Key]
        public int Id { get; set; }

        // Kết quả của cuộc hẹn nào?
        public int AppointmentId { get; set; }
        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }

        public string Diagnosis { get; set; } // Chẩn đoán bệnh
        public string? Treatment { get; set; } // Hướng điều trị/Lời dặn
        public virtual Prescription? Prescription { get; set; }
        // Ngày tái khám (Có thể null nếu khỏi hẳn)
        public DateTime? ReExaminationDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public virtual Doctor Doctor { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }
        public virtual ICollection<Prescription> Prescriptions { get; set; }
    }
}