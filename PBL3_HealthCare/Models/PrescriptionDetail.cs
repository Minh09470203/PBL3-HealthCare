using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class PrescriptionDetail
    {
        [Key]
        public int Id { get; set; }

        // Thuộc về đơn thuốc nào
        public int PrescriptionId { get; set; }
        [ForeignKey("PrescriptionId")]
        public virtual Prescription Prescription { get; set; }

        // Là thuốc gì
        public int MedicineId { get; set; }
        [ForeignKey("MedicineId")]
        public virtual Medicine Medicine { get; set; }

        public int Quantity { get; set; }

        public string Instruction { get; set; } // Cách dùng: Sáng 1 viên, Tối 1 viên

        // QUAN TRỌNG: Lưu giá tại thời điểm kê đơn 
        // (đề phòng sau này giá trong kho Medicine thay đổi thì đơn cũ không bị sai tiền)
        public decimal UnitPrice { get; set; }
    }
}