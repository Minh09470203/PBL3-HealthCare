using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Models;
using System.Linq;

namespace PBL3_HealthCare.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // =========================================================
        // KHAI BÁO CÁC BẢNG (DBSETS)
        // =========================================================
        public DbSet<Specialty> Specialties { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<Medicine> Medicines { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PrescriptionDetail> PrescriptionDetails { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceDetail> InvoiceDetails { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<HealthPackage> HealthPackages { get; set; }
        public DbSet<Vaccine> Vaccines { get; set; }
        public DbSet<HomeService> HomeServices { get; set; }
        public DbSet<PackageBooking> PackageBookings { get; set; }
        public DbSet<VaccinationBooking> VaccinationBookings { get; set; }
        public DbSet<HomeServiceRequest> HomeServiceRequests { get; set; }
        // =========================================================
        // CẤU HÌNH FLUENT API
        // =========================================================
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 1. ĐỔI TÊN BẢNG IDENTITY (Bỏ AspNet)
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (tableName != null && tableName.StartsWith("AspNet"))
                {
                    entityType.SetTableName(tableName.Substring(6));
                }
            }

            // 2. CẤU HÌNH QUAN HỆ 1-1
            builder.Entity<Appointment>()
                .HasOne(a => a.MedicalRecord)
                .WithOne(m => m.Appointment)
                .HasForeignKey<MedicalRecord>(m => m.AppointmentId);

            // 3. FIX LỖI "MULTIPLE CASCADE PATHS" (QUAN TRỌNG)
            // Chặn xóa lan truyền từ Patient/Doctor đến Appointment
            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.NoAction);

            // FIX LỖI 1785 CHO INVOICE: Cắt đứt các đường xóa lan truyền chồng chéo
            builder.Entity<Invoice>()
                .HasOne(i => i.Appointment)
                .WithOne(a => a.Invoice)
                .HasForeignKey<Invoice>(i => i.AppointmentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Nếu model Invoice của nhóm có cột MedicalRecordId, chặn nốt ở đây:
            builder.Entity<Invoice>()
                .HasOne(i => i.MedicalRecord)
                .WithMany()
                .HasForeignKey(i => i.MedicalRecordId)
                .OnDelete(DeleteBehavior.NoAction);

            // 1. Chặn xóa Bệnh nhân -> bay Lịch tiêm chủng
            builder.Entity<VaccinationBooking>()
                .HasOne(v => v.Patient)
                .WithMany()
                .HasForeignKey(v => v.PatientId)
                .OnDelete(DeleteBehavior.NoAction);

            // 2. Chặn xóa Vaccine -> bay Lịch tiêm chủng
            builder.Entity<VaccinationBooking>()
                .HasOne(v => v.Vaccine)
                .WithMany()
                .HasForeignKey(v => v.VaccineId)
                .OnDelete(DeleteBehavior.NoAction);

            // 3. Chặn xóa Bệnh nhân -> bay Lịch Y tế tại nhà
            builder.Entity<HomeServiceRequest>()
                .HasOne(h => h.Patient)
                .WithMany()
                .HasForeignKey(h => h.PatientId)
                .OnDelete(DeleteBehavior.NoAction);

            // 4. Chặn xóa Dịch vụ -> bay Lịch Y tế tại nhà
            builder.Entity<HomeServiceRequest>()
                .HasOne(h => h.HomeService)
                .WithMany()
                .HasForeignKey(h => h.HomeServiceId)
                .OnDelete(DeleteBehavior.NoAction);
            // Chặn xóa Bệnh nhân -> bay PackageBooking
            builder.Entity<PackageBooking>()
                .HasOne(p => p.Patient)
                .WithMany()
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.NoAction);

            // 4. FIX LỖI DECIMAL
            var decimalProps = builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => (System.Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal));

            foreach (var property in decimalProps)
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }
        }
    }
}