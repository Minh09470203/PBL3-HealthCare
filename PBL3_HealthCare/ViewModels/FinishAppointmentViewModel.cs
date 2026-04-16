using System;
using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.ViewModels
{
    public class FinishAppointmentViewModel
    {
        // 1. Bắt buộc phải có ID để biết đang kết thúc ca khám nào
        [Required]
        public int AppointmentId { get; set; }

        // 2. Triệu chứng của bệnh nhân
        [Required(ErrorMessage = "Vui lòng nhập triệu chứng của bệnh nhân")]
        public string Symptoms { get; set; }

        // 3. Kết luận bệnh (Chẩn đoán)
        [Required(ErrorMessage = "Vui lòng nhập kết luận chẩn đoán")]
        public string Diagnosis { get; set; }

        // 4. Lời dặn dò của Bác sĩ (Kiêng ăn gì, chườm đá...)
        public string? DoctorNotes { get; set; }

        // 5. Ngày tái khám (Nếu có)
        public DateTime? ReExaminationDate { get; set; }
    }
}