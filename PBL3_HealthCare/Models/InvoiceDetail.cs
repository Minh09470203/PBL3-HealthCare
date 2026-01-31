using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class InvoiceDetail
    {
        [Key]
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; }

        public string Content { get; set; } // Nội dung: "Phí khám" hoặc "Thuốc Panadol"

        public int Quantity { get; set; }   // Số lượng

        public decimal UnitPrice { get; set; } // Đơn giá

        // Phân loại: Dịch vụ (0) hay Thuốc (1)
        public InvoiceDetailType Type { get; set; }
    }
}