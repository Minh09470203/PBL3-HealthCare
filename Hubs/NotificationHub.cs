using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Hubs
{
    // Kế thừa Hub của SignalR
    public class NotificationHub : Hub
    {
        // Hàm này để client gọi lên, nhưng thường mình sẽ bắn từ Controller xuống
        // Tạm thời mình cứ để trống, SignalR tự lo việc duy trì kết nối

        public override async Task OnConnectedAsync()
        {
            // Có thể log ra console để biết có người vừa bật web
            // Console.WriteLine($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}