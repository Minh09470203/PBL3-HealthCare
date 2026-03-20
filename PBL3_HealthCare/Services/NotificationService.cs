using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hàm này tuần sau BE 2 sẽ gọi để bắn thông báo
        public void CreateNotification(string userId, string message)
        {
            var notification = new Notification
            {
                ReceiverId = userId, // Đổi UserId thành ReceiverId
                Content = message,   // Đổi Message thành Content
                SenderInfo = "Hệ thống", // Điền thêm cho đủ cột của ông
                Type = "Info",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();
        }
    }
}