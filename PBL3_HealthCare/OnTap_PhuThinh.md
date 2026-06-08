# BÍ KÍP ÔN TẬP BẢO VỆ ĐỒ ÁN - NGUYỄN PHÚ THỊNH
**Vai trò:** Frontend Developer (Doctor Portal, Lịch làm việc FullCalendar, Video Call, Form động)

---

## 1. MỤC TIÊU CẦN NẮM VỮNG
Bạn là người gắn các thư viện UI/UX phức tạp nhất của Frontend (FullCalendar, ZegoCloud) và xử lý giao diện có tương tác cao (Form kê đơn thêm/bớt thuốc bằng JS). 

## 2. CÂU HỎI & CÁCH TRẢ LỜI TRỌNG TÂM

### Câu 1: Bảng Lịch làm việc (Calendar) của bác sĩ được vẽ lên như thế nào?
**Trả lời:**
Dạ em tích hợp thư viện **FullCalendar v6**.
Từ Controller, Backend truyền xuống cho em một mảng danh sách Ca trực (WorkShifts) và Lịch khám (Appointments). 
Em dùng JavaScript khởi tạo đối tượng `new FullCalendar.Calendar(el, { ... })`. Sau đó em parse dữ liệu C# thành dạng JSON events và đút vào cấu hình `events: [...]` của thư viện. Em phân biệt màu sắc: nền vàng là ca trực (`background` event), màu xanh là lịch khám bệnh để bác sĩ dễ nhìn ạ.

### Câu 2: Form kê đơn thuốc em làm thế nào để bấm nút là nó tự đẻ ra thêm dòng nhập thuốc mới?
**Trả lời:**
Dạ em sử dụng kỹ thuật **Dynamic Rows bằng jQuery**.
Mỗi khi bác sĩ bấm "Thêm thuốc", em dùng sự kiện `onClick` của JS để chèn (`.append()`) một đoạn chuỗi HTML (gồm thẻ `<select>` chọn thuốc và `<input>` nhập số lượng) vào trong Form.
*Điểm mấu chốt (rất quan trọng):* Thuộc tính `name` của các thẻ input đó em phải dùng biến `index` tăng dần theo mảng C# (Ví dụ: `name="Details[0].MedicineId"`, dòng tiếp theo là `Details[1]`) thì khi Submit, Backend mới hứng được dữ liệu thành một List chuẩn ạ.

### Câu 3: Giao diện gọi Video Call (ZegoCloud) em thiết lập ở Frontend thế nào?
**Trả lời:**
Dạ em nhận `RoomID` và mã `Token` bảo mật từ Backend truyền xuống qua `ViewBag`.
Sau đó em nhúng file JS của **ZegoCloud UIKit Prebuilt** qua CDN. Em gọi hàm `ZegoUIKitPrebuilt.create(token)` và truyền các cấu hình giao diện (UI) vào như tắt/bật camera, gán role (bác sĩ là Host, bệnh nhân là Audience) để nó render ra nguyên cái giao diện gọi Video full màn hình ạ.

## 3. FILE CODE CẦN ĐỌC LẠI
- `Views/Schedules/Index.cshtml` (Đọc kỹ đoạn code JS khởi tạo FullCalendar).
- `Views/Prescriptions/Create.cshtml` (Xem lại hàm JavaScript `addMedicineRow()` dùng `.append()`).
- `Views/VideoCall/Room.cshtml` (Xem lại code khởi tạo ZegoCloud UIKit).
