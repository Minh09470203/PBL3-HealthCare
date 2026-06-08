# BÍ KÍP ÔN TẬP BẢO VỆ ĐỒ ÁN - NGUYỄN PHÚ MINH THÁI
**Vai trò:** Frontend Developer (Giao diện Khách, Admin Dashboard, Chatbot AI)

---

## 1. MỤC TIÊU CẦN NẮM VỮNG
Bạn là người làm "mặt tiền" của website. Thầy cô sẽ hỏi bạn về cấu trúc HTML, cách chia Layout, cách tùy chỉnh CSS và cách sử dụng các thư viện biểu đồ.

## 2. CÂU HỎI & CÁCH TRẢ LỜI TRỌNG TÂM

### Câu 1: Em chia Layout cho trang web như thế nào trong ASP.NET Core?
**Trả lời:** 
Dạ, em sử dụng cơ chế Layout của Razor View. Em tạo ra các file `_HomeLayout.cshtml`, `_AdminLayout.cshtml` đặt trong thư mục `Views/Shared/`. 
Các file này chứa phần khung cố định (Header, Menu, Footer). Ở giữa file Layout em dùng hàm `@RenderBody()`. 
Khi chạy các trang con (như `Index.cshtml`), nó sẽ tự động chèn nội dung của trang con vào vị trí `@RenderBody()` đó, giúp em không phải copy lại code HTML nhiều lần ạ.

### Câu 2: Biểu đồ (Chart) trong trang Admin hoạt động như thế nào?
**Trả lời:**
Dạ em sử dụng thư viện **Chart.js**. 
Đầu tiên ở Backend, bạn Minh trả về cho em dữ liệu dưới dạng JSON (mảng doanh thu hoặc số lượng bệnh nhân).
Sau đó ở Frontend, em dùng thẻ `<canvas>` để tạo không gian vẽ. Em dùng JavaScript gọi hàm `new Chart()` truyền cục data JSON đó vào, cấu hình type là `'bar'` (biểu đồ cột) hoặc `'doughnut'` (biểu đồ tròn) để Chart.js tự động vẽ ra giao diện ạ.

### Câu 3: Giao diện Chatbot AI em làm như thế nào?
**Trả lời:**
Em thiết kế bằng HTML/CSS thuần (file `_Chatbot.cshtml`). Em dùng CSS thuộc tính `position: fixed; bottom: 20px; right: 20px;` để neo cái bong bóng chat luôn nổi ở góc phải màn hình.
Khi người dùng bấm vào bong bóng, em dùng hàm `toggle()` của JavaScript để hiện ra cửa sổ chat. Phần nội dung chat bên trong dùng thẻ `<div>` cuộn dọc (`overflow-y: auto`) để hiển thị các tin nhắn của người dùng và AI ạ.

## 3. FILE CODE CẦN ĐỌC LẠI
- `Views/Shared/_HomeLayout.cshtml`, `_AdminLayout.cshtml`
- `Views/Admin/Index.cshtml` (Đọc kỹ đoạn thẻ `<canvas>` và đoạn `<script>` gọi Chart.js ở dưới cùng).
- `Views/Shared/_Chatbot.cshtml` (Xem lại CSS position cố định và JS toggle).
