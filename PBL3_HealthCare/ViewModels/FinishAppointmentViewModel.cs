using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.ViewModels
{
    public class FinishAppointmentViewModel
    {
        [Required]
        public int AppointmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập triệu chứng của bệnh nhân")]
        public string Symptoms { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập kết luận chẩn đoán")]
        public string Diagnosis { get; set; } //Kết luận bệnh

        public string? Prescription { get; set; } //Kê đơn thuốc

        public string? DoctorNotes { get; set; }
    }
}