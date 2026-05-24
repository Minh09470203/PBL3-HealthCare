using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            try
            {
                // Lấy cấu hình từ file appsettings.json
                var emailSettings = _config.GetSection("EmailSettings");
                var mail = emailSettings["Mail"];
                var displayName = emailSettings["DisplayName"];
                var password = emailSettings["Password"];
                var host = emailSettings["Host"];
                var port = int.Parse(emailSettings["Port"]);

                // Cấu hình nội dung Email
                var message = new MailMessage
                {
                    From = new MailAddress(mail, displayName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true // Bật cái này lên để gửi được HTML (có màu sắc, nút bấm)
                };
                message.To.Add(new MailAddress(toEmail));

                // Cấu hình server gửi (SMTP của Google)
                using var smtpClient = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(mail, password),
                    EnableSsl = true, // Phải có SSL Google mới cho gửi
                    Timeout = 10000 // Timeout 10 giây (dùng cho Send đồng bộ)
                };

                // Render Free Tier chặn port 587, dẫn đến SendMailAsync bị treo vô hạn.
                // Ta dùng Task.WhenAny để ép timeout sau 10 giây.
                var sendTask = smtpClient.SendMailAsync(message);
                var timeoutTask = Task.Delay(10000); // 10 giây timeout

                var completedTask = await Task.WhenAny(sendTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    // Quá thời gian 10s (chắc chắn là do Render chặn Port)
                    return false;
                }

                await sendTask; // Ném lỗi nếu có (sai pass, vv)
                return true;
            }
            catch (System.Exception)
            {
                // Bắt mọi lỗi (sai mật khẩu, kết nối bị từ chối...)
                return false;
            }
        }
    }
}