using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Schedule
    {
        [Key]
        public int Id { get; set; }

        public int DoctorId { get; set; }
        [ForeignKey("DoctorId")]
        public Doctor? Doctor { get; set; }

        public DateTime Date { get; set; } // Ngày trực
        public string Shift { get; set; } = "Morning"; // Ca: Morning/Afternoon
        public bool IsAvailable { get; set; } = true;
    }
}