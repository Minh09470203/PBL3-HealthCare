using Microsoft.AspNetCore.Identity;

namespace PBL3_HealthCare.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Gender { get; set; }
    }
}