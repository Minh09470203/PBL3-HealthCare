using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        // Hóa đơn của cuộc hẹn nào
        public int AppointmentId { get; set; }
        [ForeignKey("AppointmentId")]
        public virtual Appointment Appointment { get; set; }

        public decimal TotalAmount { get; set; } // Tổng tiền khách phải trả

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Chi tiết hóa đơn (Các dòng tiền)
        public virtual ICollection<InvoiceDetail> Details { get; set; }
        public int? MedicalRecordId { get; set; }
        public virtual MedicalRecord MedicalRecord { get; set; }

        public string? Note { get; set; }
    }

    // Định nghĩa Enum cho Status
}