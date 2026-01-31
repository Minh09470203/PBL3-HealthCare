using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Prescription
    {
        [Key]
        public int Id { get; set; }

        // Liên kết: Đơn thuốc này thuộc về Lần khám nào?
        public int MedicalRecordId { get; set; }
        [ForeignKey("MedicalRecordId")]
        public virtual MedicalRecord MedicalRecord { get; set; }

        public string? Note { get; set; } // Lời dặn chung của dược sĩ (nếu có)

        public PrescriptionStatus Status { get; set; } = PrescriptionStatus.New;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Danh sách thuốc trong đơn
        public virtual ICollection<PrescriptionDetail> Details { get; set; }
    }
}