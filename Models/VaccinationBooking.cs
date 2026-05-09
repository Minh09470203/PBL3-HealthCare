namespace PBL3_HealthCare.Models
{
    public class VaccinationBooking
    {
        public int Id { get; set; }
        public string PatientId { get; set; }
        public ApplicationUser Patient { get; set; } // Trỏ về User
        public int VaccineId { get; set; }
        public Vaccine Vaccine { get; set; }
        public DateTime BookingDate { get; set; }
        public string Status { get; set; } // Pending, Completed, Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
