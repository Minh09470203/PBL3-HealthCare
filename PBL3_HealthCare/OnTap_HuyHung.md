# BÍ KÍP ÔN TẬP BẢO VỆ ĐỒ ÁN - NGUYỄN HUY HÙNG
**Vai trò:** Backend Developer (Nghiệp vụ Y tế, Đặt lịch, Transaction, Email, Master Data CRUD)

---

## 1. MỤC TIÊU CẦN NẮM VỮNG
Bạn là người nắm "Logic kinh doanh" (Business Logic) cốt lõi của ứng dụng. Thầy cô sẽ hỏi bạn về luồng dữ liệu HTTP GET/POST, cách ràng buộc an toàn dữ liệu (Validation) và cách giải quyết các nghiệp vụ khó như Kê đơn thuốc (cần dùng Transaction).

## 2. CÂU HỎI & CÁCH TRẢ LỜI TRỌNG TÂM

### Câu 1: Trong Controller, tại sao lại có hàm GET và POST trùng tên nhau (Ví dụ: `Create`)?
**Trả lời:**
Dạ vì trên Web phải trải qua 2 bước riêng biệt:
- Hàm **`[HttpGet]`**: Dùng để render (mở) cái giao diện HTML trống ra màn hình cho người dùng xem và điền thông tin.
- Hàm **`[HttpPost]`**: Chạy khi người dùng bấm nút Submit (Lưu). Nó có nhiệm vụ "chộp" lấy khối dữ liệu người dùng vừa gõ vào form, mang đi kiểm tra lỗi (Validate), và cuối cùng là lưu xuống Database (`_context.SaveChanges()`).

### Câu 2: Khi bác sĩ Kê đơn thuốc, em xử lý trừ số lượng tồn kho như thế nào để đảm bảo an toàn?
**Trả lời (CÂU NÀY CỰC QUAN TRỌNG ĐỂ LẤY ĐIỂM CAO):**
Dạ khi kê đơn, một đơn có thể chứa nhiều loại thuốc. Nếu dùng vòng lặp lưu bình thường, lỡ đến loại thuốc thứ 3 bị hết hàng, chương trình báo lỗi thì 2 thuốc đầu tiên đã bị lưu oan uổng vào DB.
Để giải quyết, em áp dụng **Database Transaction**. 
Em gọi lệnh `await transaction.BeginTransactionAsync()`. Em nhét toàn bộ vòng lặp lưu thuốc và trừ kho vào trong khối lệnh này. Nếu có bất kỳ loại thuốc nào kho không đủ, em lập tức gọi `transaction.RollbackAsync()` để hủy bỏ mọi thay đổi trước đó, giữ cho Database luôn ở trạng thái nguyên vẹn. Chỉ khi mọi thứ ok 100% em mới gọi `transaction.CommitAsync()` để chốt lưu ạ.

### Câu 3: Logic gửi Email tự động chạy như thế nào?
**Trả lời:**
Dạ em viết 1 cái `EmailService`. Ở trong đó em dùng thư viện `SmtpClient` của System.Net.Mail, kết nối đến cổng `587` của smtp.gmail.com bằng tài khoản app password của phòng khám.
Để Email trông đẹp mắt, em tách phần giao diện thư ra file `.html` (Template). Ở Controller, em dùng lệnh `File.ReadAllText()` đọc file html đó lên, dùng lệnh `.Replace("{{PatientName}}", tên_bệnh_nhân)` để tráo dữ liệu người thật vào, xong gán vào Body của SmtpClient và gửi đi ạ.

### Câu 4: Em xử lý trùng giờ khám (Conflict) như thế nào khi bệnh nhân đặt lịch?
**Trả lời:**
Dạ trong hàm `BookAppointment`, em viết 1 câu Query (LINQ) kiểm tra bảng `Appointments` xem có tồn tại lịch hẹn nào có cùng `DoctorId`, cùng `Date`, cùng `TimeSlot` và trạng thái khác "Hủy" hay không. Nếu hàm `.Any()` trả về true, em dùng `ModelState.AddModelError` để chặn form lại và báo lỗi "Giờ này đã có người đặt" không cho lưu ạ.

## 3. FILE CODE CẦN ĐỌC LẠI
- `Controllers/PrescriptionsController.cs` (Kéo xuống hàm Create [HttpPost], đọc kỹ khúc `BeginTransaction` và Rollback).
- `Controllers/AppointmentsController.cs` (Kéo xuống đoạn gửi Email xem cách đọc file HTML Template và Replace).
- `Controllers/HomeController.cs` (Xem lại hàm BookAppointment đoạn bắt lỗi trùng lịch).
