using Microsoft.AspNetCore.Identity;
using PBL3_HealthCare.Models;

namespace PBL3_HealthCare.Data
{
    public static class DbSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. TẠO ROLES (Quyền)
            string[] roles = { "Admin", "Doctor", "Patient" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. TẠO TÀI KHOẢN ADMIN MẶC ĐỊNH
            var adminEmail = "admin@gmail.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Quản trị viên Hệ thống",
                    EmailConfirmed = true // Bỏ qua bước xác nhận email
                };

                // Mật khẩu bắt buộc có chữ hoa, chữ thường, số và ký tự đặc biệt
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 3. TẠO CHUYÊN KHOA (Specialties)
            if (!context.Specialties.Any()) // Nếu bảng chưa có dữ liệu thì mới thêm
            {
                context.Specialties.AddRange(
                    new Specialty { Name = "Nội khoa", Description = "Khám các bệnh lý nội khoa chung" },
                    new Specialty { Name = "Ngoại khoa", Description = "Khám và phẫu thuật ngoại khoa" },
                    new Specialty { Name = "Nhi khoa", Description = "Khám và điều trị cho trẻ em" },
                    new Specialty { Name = "Da liễu", Description = "Khám và điều trị các bệnh về da" }
                );
                await context.SaveChangesAsync();
            }

            // 4. TẠO THUỐC MẪU (Medicines)
            if (!context.Medicines.Any())
            {
                context.Medicines.AddRange(
                    new Medicine { Name = "Panadol Extra", StockQuantity = 1000, Unit = "Viên", Price = 2000 },
                    new Medicine { Name = "Vitamin C 500mg", StockQuantity = 500, Unit = "Vỉ", Price = 15000 },
                    new Medicine { Name = "Amoxicillin 500mg", StockQuantity = 300, Unit = "Hộp", Price = 50000 },
                    new Medicine { Name = "Oresol", StockQuantity = 200, Unit = "Gói", Price = 5000 }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}