using System;
using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.ViewModels
{
    public class WalkInViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên bệnh nhân")]
        [Display(Name = "Họ và tên")]
        public string PatientName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn bác sĩ")]
        [Display(Name = "Bác sĩ phụ trách")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày khám")]
        [Display(Name = "Ngày khám")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn giờ khám")]
        [Display(Name = "Giờ khám")]
        public TimeSpan TimeSlot { get; set; } = DateTime.Now.TimeOfDay;

        [Display(Name = "Lý do khám")]
        public string? Reason { get; set; }
    }
}
