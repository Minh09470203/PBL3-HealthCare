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

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
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
                EnableSsl = true // Phải có SSL Google mới cho gửi
            };

            await smtpClient.SendMailAsync(message);
        }
    }
}