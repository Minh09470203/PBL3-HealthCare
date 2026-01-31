using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public string? Avatar { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}