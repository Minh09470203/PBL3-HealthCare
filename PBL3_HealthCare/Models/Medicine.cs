using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.Models
{
    public class Medicine
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên thuốc không được để trống")]
        public string Name { get; set; }

        public int StockQuantity { get; set; } // Số lượng tồn kho

        public string Unit { get; set; }       // Đơn vị tính: Viên, Vỉ, Hộp, Chai

        public decimal Price { get; set; }     // Giá bán hiện tại
    }
}