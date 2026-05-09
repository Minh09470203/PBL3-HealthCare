using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PBL3_HealthCare.Models
{
    public class Notification
    {
        [Key] // <--- QUAN TRỌNG: Phải có dòng này thì mới tạo được bảng
        public int Id { get; set; }

        public string ReceiverId { get; set; } // Người nhận thông báo
        [ForeignKey("ReceiverId")]
        public virtual ApplicationUser Receiver { get; set; }

        public string? SenderInfo { get; set; } // Ví dụ: "Bác sĩ Tuấn", "Hệ thống"

        public string? Content { get; set; }    // Nội dung: "Bạn có lịch hẹn mới"

        public string? Link { get; set; }       // Link bấm vào để xem chi tiết

        public string? Type { get; set; }       // Loại: "Info", "Warning"...

        public bool IsRead { get; set; } = false; // Đã xem chưa

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}