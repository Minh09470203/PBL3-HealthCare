using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Models; // Đảm bảo namespace này đúng với project của bạn

namespace PBL3_HealthCare.Data
{
    // Kế thừa từ IdentityDbContext để có sẵn bảng User/Role
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // =========================================================
        // KHAI BÁO CÁC BẢNG (DBSETS)
        // =========================================================

        // 1. Nhóm Bác sĩ & Lịch
        public DbSet<Specialty> Specialties { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        // 2. Nhóm Y tế (Khám & Thuốc)
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<Medicine> Medicines { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PrescriptionDetail> PrescriptionDetails { get; set; }

        // 3. Nhóm Tài chính & Thông báo
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceDetail> InvoiceDetails { get; set; }
        public DbSet<Notification> Notifications { get; set; }


        // =========================================================
        // CẤU HÌNH FLUENT API (LOGIC DATABASE)
        // =========================================================
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // BẮT BUỘC PHẢI CÓ dòng này để Identity chạy

            // -----------------------------------------------------
            // 1. ĐỔI TÊN BẢNG IDENTITY (Bỏ tiền tố AspNet cho gọn)
            // -----------------------------------------------------
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (tableName.StartsWith("AspNet"))
                {
                    entityType.SetTableName(tableName.Substring(6));
                }
            }

            // -----------------------------------------------------
            // 2. CẤU HÌNH QUAN HỆ 1-1 (One-to-One Relationships)
            // -----------------------------------------------------

            // Appointment (1) <---> (1) MedicalRecord
            builder.Entity<Appointment>()
                .HasOne(a => a.MedicalRecord)
                .WithOne(m => m.Appointment)
                .HasForeignKey<MedicalRecord>(m => m.AppointmentId);

            // Appointment (1) <---> (1) Invoice
            builder.Entity<Appointment>()
                .HasOne(a => a.Invoice)
                .WithOne(i => i.Appointment)
                .HasForeignKey<Invoice>(i => i.AppointmentId);

            // -----------------------------------------------------
            // 3. FIX LỖI "MULTIPLE CASCADE PATHS" (QUAN TRỌNG)
            // SQL Server không cho phép xóa lan truyền vòng tròn
            // -----------------------------------------------------

            // Khi xóa Bệnh nhân -> KHÔNG tự động xóa Lịch hẹn (Set Null hoặc NoAction)
            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.NoAction);

            // Khi xóa Bác sĩ -> KHÔNG tự động xóa Lịch hẹn
            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments) 
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.NoAction);

            // -----------------------------------------------------
            // 4. FIX LỖI DECIMAL (TIỀN NONG)
            // Tự động tìm tất cả các cột kiểu decimal để set độ chính xác (18,2)
            // -----------------------------------------------------
            var decimalProps = builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => (System.Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal));

            foreach (var property in decimalProps)
            {
                property.SetPrecision(18); // Tổng 18 số
                property.SetScale(2);      // 2 số thập phân (VD: 100.50)
            }
        }
    }
}