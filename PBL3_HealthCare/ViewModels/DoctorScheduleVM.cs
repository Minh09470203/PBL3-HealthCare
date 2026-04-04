namespace PBL3_HealthCare.ViewModels
{
    public class DoctorScheduleVM
    {
        // Danh sách các ca trực (Khung giờ làm việc do Admin giao)
        public List<PBL3_HealthCare.Models.Schedule> WorkShifts { get; set; }

        // Danh sách các ca khám cụ thể (Có bệnh nhân đặt)
        public List<PBL3_HealthCare.Models.Appointment> PatientAppointments { get; set; }
    }
}