using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Models; // <--- Đảm bảo dòng này đúng tên project của bạn

namespace PBL3_HealthCare.Data
{
    // Kế thừa từ IdentityDbContext để có sẵn bảng User/Role
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Đăng ký các bảng mới
        public DbSet<Specialty> Specialties { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. Đổi tên bảng Identity (Tùy chọn, giữ nguyên cũng được)
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (tableName.StartsWith("AspNet"))
                {
                    entityType.SetTableName(tableName.Substring(6));
                }
            }

            // 2. FIX LỖI "MULTIPLE CASCADE PATHS" (QUAN TRỌNG NHẤT) <---
            // Khi xóa Bệnh nhân, KHÔNG tự động xóa Lịch hẹn (phải xóa tay)
            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.NoAction); // <--- Chặn xóa lan truyền tại đây

            // Khi xóa Bác sĩ, KHÔNG tự động xóa Lịch hẹn
            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.NoAction);

            // 3. FIX LỖI CẢNH BÁO DECIMAL (TIỀN NONG)
            builder.Entity<Doctor>()
                .Property(d => d.Price)
                .HasColumnType("decimal(18,2)");
        }
    }
}