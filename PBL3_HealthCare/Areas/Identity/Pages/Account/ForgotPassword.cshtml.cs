using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using PBL3_HealthCare.Models;
// 🚨 NHỚ THÊM DÒNG NÀY ĐỂ GỌI SHIPPER
using PBL3_HealthCare.Services;

namespace PBL3_HealthCare.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        // 🚨 THAY IEmailSender bằng EmailService của mình
        private readonly EmailService _emailService;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, EmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
            [Display(Name = "Tên đăng nhập")]
            public string UserName { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(Input.UserName);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản không tồn tại trong hệ thống.");
                    return Page();
                }

                if (user.Email == null || user.Email.EndsWith("@system.local"))
                {
                    ModelState.AddModelError(string.Empty, "Tài khoản của bạn chưa được liên kết với địa chỉ Email thực tế. Vui lòng đăng nhập và cập nhật Email trong Hồ sơ cá nhân, hoặc liên hệ Admin.");
                    return Page();
                }

                if (!(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    ModelState.AddModelError(string.Empty, "Email liên kết của bạn chưa được xác thực. Không thể khôi phục mật khẩu lúc này. Vui lòng đăng nhập vào tài khoản và yêu cầu gửi lại link xác thực Email, hoặc liên hệ Admin.");
                    return Page();
                }

                // 1. TẠO LINK ĐẶT LẠI MẬT KHẨU
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                // 2. GÓI VÀO TEMPLATE HTML CỰC ĐẸP (MÀU ĐỎ CẢNH BÁO)
                string mailBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #f4f7f6;'>
                        <div style='max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 4px 8px rgba(0,0,0,0.1); border-top: 5px solid #E03131;'>
                            <h2 style='color: #E03131; text-align: center;'>YÊU CẦU ĐẶT LẠI MẬT KHẨU</h2>
                            <p>Chào <strong>{user.FullName ?? "bạn"}</strong>,</p>
                            <p>Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản Phòng Khám SuperStar của bạn. Vui lòng bấm vào nút bên dưới để tiến hành tạo mật khẩu mới:</p>
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' style='background-color: #E03131; color: white; padding: 14px 28px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px; display: inline-block;'>ĐẶT LẠI MẬT KHẨU NGAY</a>
                            </div>
                            <p style='color: #777; font-size: 13px; text-align: center;'>Nếu bạn không yêu cầu đổi mật khẩu, vui lòng bỏ qua email này. Tuyệt đối không chia sẻ đường link này cho bất kỳ ai!</p>
                        </div>
                    </div>";

                // 3. GIAO CHO SHIPPER BẮN QUA GMAIL
                await _emailService.SendEmailAsync(user.Email, "Thiết lập lại mật khẩu SuperStar", mailBody);

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }
    }
}