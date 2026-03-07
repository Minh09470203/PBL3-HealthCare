using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Models;

namespace PBL3_HealthCare.Data;

public class PBL3_HealthCareContext : IdentityDbContext<ApplicationUser>
{
    public PBL3_HealthCareContext(DbContextOptions<PBL3_HealthCareContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);
    }
}
