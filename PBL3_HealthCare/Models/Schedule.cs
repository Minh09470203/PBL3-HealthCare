using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Schedule
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Bác sĩ")]
        public int DoctorId { get; set; }

        [ForeignKey("DoctorId")]
        public virtual Doctor? Doctor { get; set; } // Thêm 'virtual' để hỗ trợ Lazy Loading nếu cần

        [Display(Name = "Ngày trực")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime Date { get; set; }

        [Display(Name = "Ca trực")]
        public string Shift { get; set; } = "Morning";

        [Display(Name = "Trạng thái")]
        public bool IsAvailable { get; set; } = true;
    }
}