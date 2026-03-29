using Microsoft.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;

using PBL3_HealthCare.Data;

using PBL3_HealthCare.Models;

using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();



builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)

    .AddRoles<IdentityRole>()

    .AddEntityFrameworkStores<ApplicationDbContext>();
// Thêm code này vào Program.cs
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        IConfigurationSection googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
        options.ClientId = googleAuthNSection["ClientId"];
        options.ClientSecret = googleAuthNSection["ClientSecret"];
    });
builder.Services.AddControllersWithViews();
// Đăng ký NotificationService để các Controller khác gọi được
builder.Services.AddScoped<PBL3_HealthCare.Services.NotificationService>();
// Đăng ký các Service tự viết
builder.Services.AddScoped<PBL3_HealthCare.Services.InvoiceService>();
// Đăng ký Service AI vào hệ thống
builder.Services.AddScoped<PBL3_HealthCare.Services.GeminiService>();

var app = builder.Build();



using (var scope = app.Services.CreateScope())

{

    var services = scope.ServiceProvider;

    try

    {

        await DbSeeder.SeedDataAsync(services);

    }

    catch (Exception ex)

    {

        // Ghi log ra màn hình console nếu có lỗi lúc seed data

        var logger = services.GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Có lỗi xảy ra khi khởi tạo dữ liệu mẫu.");

    }

}



// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())

{

    app.UseMigrationsEndPoint();

}

else

{

    app.UseExceptionHandler("/Home/Error");

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.

    app.UseHsts();

}



app.UseHttpsRedirection();

app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();



app.MapStaticAssets();



app.MapControllerRoute(

    name: "default",

    pattern: "{controller=Home}/{action=Index}/{id?}")

    .WithStaticAssets();



app.MapRazorPages()

   .WithStaticAssets();



app.Run();