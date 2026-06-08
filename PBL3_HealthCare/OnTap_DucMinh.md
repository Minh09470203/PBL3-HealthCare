# BÍ KÍP ÔN TẬP BẢO VỆ ĐỒ ÁN - NGUYỄN ĐỨC MINH
**Vai trò:** Backend Developer & System Architect (DB, Identity, SignalR, External APIs)

---

## 1. MỤC TIÊU CẦN NẮM VỮNG
Bạn là người thiết kế lõi hệ thống. Thầy cô sẽ xoáy mạnh vào Kiến trúc (MVC), Cơ sở dữ liệu (EF Core Code-First), Bảo mật (Identity) và cách tích hợp các API nền tảng.

## 2. CÂU HỎI & CÁCH TRẢ LỜI TRỌNG TÂM

### Câu 1: Nhóm em thiết kế Database theo phương pháp nào? Ưu điểm?
**Trả lời:**
Dạ nhóm em sử dụng Entity Framework Core với phương pháp **Code-First**.
Tức là em không mở SQL Server lên tạo bảng thủ công, mà em viết các class C# (Entity Models như `Appointment`, `Doctor`). Sau đó cấu hình quan hệ bằng Fluent API hoặc Data Annotations.
Cuối cùng em chạy lệnh `Add-Migration` và `Update-Database`.
*Ưu điểm:* Dễ quản lý phiên bản database bằng code, dễ làm việc nhóm (chỉ cần pull code về chạy lệnh là có ngay DB giống nhau), code C# và DB luôn đồng bộ tuyệt đối ạ.

### Câu 2: Chức năng thông báo (Notification) realtime hoạt động ra sao?
**Trả lời:**
Dạ em sử dụng thư viện **SignalR** hỗ trợ giao thức WebSockets.
Ở Backend, em tạo 1 class `NotificationHub` kế thừa từ `Hub`. Khi có sự kiện (ví dụ Admin duyệt lịch), Controller gọi hàm `IHubContext.Clients.All.SendAsync("ReceiveNotification", userId, message)`.
Lúc này SignalR sẽ duy trì một đường ống kết nối mở (TCP/WebSockets) với tất cả trình duyệt của khách hàng, nó bắn luồng dữ liệu thẳng xuống Frontend ngay lập tức mà không cần người dùng phải bấm F5 (Tải lại trang) ạ.

### Câu 3: Em tích hợp AI Chatbot (Google Gemini) như thế nào?
**Trả lời:**
Dạ em sử dụng REST API để gọi model Gemini 2.5 Flash của Google.
Em viết một cái `GeminiService`, gửi POST Request chứa chuỗi JSON (trong đó có Lịch sử chat và System Prompt do nhóm em tự định nghĩa). 
Để AI có thể tư vấn bác sĩ, em đã áp dụng kỹ thuật **Prompt Engineering**: Em truyền sẵn danh sách bác sĩ của phòng khám vào System Prompt, và ra lệnh cho AI nếu thấy phù hợp thì xuất ra mã format `[BOOKING:ID|Tên]`. Frontend sẽ bắt lấy đoạn mã này biến thành UI Card đặt lịch.

### Câu 4: Dependency Injection (DI) là gì và em xài ở đâu?
**Trả lời:**
Dạ DI (Tiêm phụ thuộc) là kỹ thuật quản lý việc khởi tạo Object, tránh dùng từ khóa `new` lung tung gây tốn bộ nhớ. Em đăng ký các Service ở `Program.cs` bằng `builder.Services.AddScoped<IEmailService, EmailService>()`. Khi Controller cần dùng, em chỉ cần khai báo ở Hàm dựng (Constructor), ASP.NET Core sẽ tự động tiêm (inject) công cụ đó vào tay Controller cho nó xài ạ.

## 3. FILE CODE CẦN ĐỌC LẠI
- `Program.cs` (Đọc các dòng builder.Services... để hiểu DI).
- Thư mục `Services/` (Mở `GeminiService.cs`, `ZegoTokenService.cs`, `NotificationHub.cs` ra xem).
- `Data/ApplicationDbContext.cs` (Xem các DbSet).
