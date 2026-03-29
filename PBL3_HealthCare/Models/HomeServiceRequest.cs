namespace PBL3_HealthCare.Models
{
    public class HomeServiceRequest
    {
        public int Id { get; set; }
        public string PatientId { get; set; }
        public ApplicationUser Patient { get; set; }
        public int HomeServiceId { get; set; }
        public HomeService HomeService { get; set; }
        public string Address { get; set; } // Địa chỉ khách yêu cầu tới
        public string Phone { get; set; } // SĐT liên hệ
        public DateTime RequestDate { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
