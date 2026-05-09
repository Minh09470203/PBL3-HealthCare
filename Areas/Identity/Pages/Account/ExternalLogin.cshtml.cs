using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using PBL3_HealthCare.Models; // Sử dụng model ApplicationUser của bạn

namespace PBL3_HealthCare.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }
        public string ReturnUrl { get; set; }
        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        // Hàm này kích hoạt khi bạn bấm nút "Đăng nhập với Google"
        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        // HÀM XỬ LÝ KHI GOOGLE TRẢ KẾT QUẢ VỀ (ĐÃ NÂNG CẤP ĐĂNG NHẬP 1 CHẠM)
        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Lỗi từ dịch vụ bên thứ 3: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Không lấy được thông tin từ Google.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // 1. Kiểm tra xem tài khoản này đã liên kết từ trước chưa
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                string existingEmail = info.Principal.FindFirstValue(ClaimTypes.Email);
                var existingUser = await _userManager.FindByEmailAsync(existingEmail);

                if (existingUser != null && !await _userManager.IsInRoleAsync(existingUser, "Patient"))
                {
                    await _userManager.AddToRoleAsync(existingUser, "Patient");

                    // Gán quyền xong phải "F5" lại phiên đăng nhập để Cookie nhận thẻ quyền mới
                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                }
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }

            // 2. Nếu chưa liên kết -> Tự động sinh tài khoản ngầm và đăng nhập luôn
            if (!result.IsLockedOut)
            {
                if (!info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
                {
                    ErrorMessage = "Google không cung cấp địa chỉ Email. Vui lòng thử cách khác.";
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }

                string email = info.Principal.FindFirstValue(ClaimTypes.Email);
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    // Tạo tài khoản ngầm với tên thật lấy từ Google
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FullName = info.Principal.Identity.Name ?? "Người dùng Google",
                        EmailConfirmed = true
                    };


                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        foreach (var error in createResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        ErrorMessage = "Có lỗi xảy ra khi tạo tài khoản ngầm. Vui lòng thử lại.";
                        return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                    }
                }
                await _userManager.AddToRoleAsync(user, "Patient");
                // Liên kết tài khoản Google với User vừa tìm/tạo
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                    _logger.LogInformation("Tài khoản {Email} đã được tạo ngầm và liên kết Google.", email);
                    return LocalRedirect(returnUrl); // Bỏ qua trang xác nhận, chui thẳng vào Web
                }
            }

            ErrorMessage = "Đăng nhập Google thất bại vì lý do bảo mật.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Hàm dự phòng (Giữ nguyên cấu trúc của Identity)
        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Lỗi tải thông tin bên ngoài trong quá trình xác nhận.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, FullName = info.Principal.Identity.Name ?? "Người dùng Google" };
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }
    }
}