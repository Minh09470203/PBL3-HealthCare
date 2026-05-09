using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides; // ✅ 1. THÊM THƯ VIỆN NÀY
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Hubs;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ✅ 2. THÊM ĐOẠN NÀY: CẤU HÌNH NHẬN DIỆN HTTPS TỪ PROXY CỦA RENDER
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Xóa rào cản IP proxy vì Render dùng IP động, không clear là nó chặn
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddGoogle(options =>
{
    IConfigurationSection googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
    options.ClientId = googleAuthNSection["ClientId"];
    options.ClientSecret = googleAuthNSection["ClientSecret"];
    // ✅ Fix cookie trên môi trường HTTPS proxy
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.HttpOnly = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PBL3_HealthCare.Services.NotificationService>();
builder.Services.AddScoped<PBL3_HealthCare.Services.InvoiceService>();
builder.Services.AddScoped<ZegoTokenService>();

// ✅ Chatbot services
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GeminiService>();

// 🔥 KHỞI ĐỘNG DỊCH VỤ SIGNALR LÊN SERVER
builder.Services.AddSignalR();

// ✅ Fix Data Protection cho môi trường deploy
var isDevelopment = builder.Environment.IsDevelopment();
var keysPath = isDevelopment
    ? Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")
    : "/tmp/DataProtection-Keys";

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("PBL3_HealthCare");

var app = builder.Build();

// ✅ 3. THÊM DÒNG NÀY: KÍCH HOẠT NHẬN DIỆN HTTPS (Bắt buộc phải nằm ngay sau Build)
app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        await DbSeeder.SeedDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Có lỗi xảy ra khi khởi tạo dữ liệu mẫu.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

// 🔥 MỞ ĐƯỜNG ỐNG ROUTING CHO CÁI HUB
app.MapHub<NotificationHub>("/notificationHub");

app.Run();