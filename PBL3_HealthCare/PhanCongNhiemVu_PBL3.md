# BẢNG PHÂN CÔNG NHIỆM VỤ DỰ ÁN PBL3 - HEALTHCARE CLINIC
*Tài liệu ôn tập dành cho ngày bảo vệ Đồ án môn học*

---

## 1. NGUYỄN PHÚ MINH THÁI (FRONTEND DEVELOPER)
*Vai trò: Chịu trách nhiệm thiết kế giao diện chính cho Khách hàng (Patient), trang Quản trị (Admin) và Giao diện Chatbot AI.*

**Công việc cụ thể đã thực hiện:**
- **Layout dùng chung:** Thiết kế và tùy chỉnh CSS cho `_HomeLayout.cshtml` (Giao diện khách), `_AdminLayout.cshtml` (Dashboard xanh gradient) và `_PatientLayout.cshtml` (Portal bệnh nhân).
- **Trang chủ & Đặt lịch (Home Views):**
  - Xây dựng Landing page (`Index.cshtml`), Carousel dịch vụ, Banner.
  - Danh sách Bác sĩ (`DoctorList.cshtml`) và Chi tiết hồ sơ bác sĩ (`DoctorInfo.cshtml`).
  - Giao diện form điền thông tin đặt lịch khám (`BookAppointment.cshtml`).
- **Giao diện Admin Dashboard:**
  - Tích hợp biểu đồ thống kê **Chart.js** hiển thị Doanh thu 6 tháng và Tỷ lệ bệnh nhân theo chuyên khoa.
- **Trợ lý Ảo AI (Chatbot):**
  - Tự code toàn bộ UI/UX bằng HTML/CSS/JS cho khung chat bong bóng nổi ở góc phải màn hình (`_Chatbot.cshtml`).
- **Các dịch vụ mở rộng (Services Views):** Xây dựng giao diện giới thiệu Gói khám sức khỏe, Tiêm chủng, Y tế tại nhà.
- **Styling:** Custom toàn bộ CSS cho các module để đồng bộ nhận diện thương hiệu của phòng khám.

---

## 2. NGUYỄN PHÚ THỊNH (FRONTEND DEVELOPER)
*Vai trò: Phụ trách giao diện làm việc của Bác sĩ (Doctor Portal), hiển thị Lịch trình trực quan, và giao diện Khám bệnh Video Call.*

**Công việc cụ thể đã thực hiện:**
- **Doctor Portal Views:** 
  - Giao diện xem danh sách bệnh nhân hôm nay, Bảng điều khiển Bác sĩ (`DoctorInfo`, `Profile.cshtml`).
- **Tích hợp Thư viện Lịch (FullCalendar):**
  - Nhúng và cấu hình plugin `FullCalendar.io` để vẽ Lịch làm việc (`MySchedule`) hiển thị rõ Slot trống (xanh), Slot đã đặt (đỏ), Ca trực (vàng).
- **Giao diện Nghiệp vụ Khám bệnh:**
  - Quản lý danh sách Lịch khám (`Appointments/Index.cshtml`, `Appointments/Details.cshtml`).
  - Form Kê đơn thuốc động (`Prescriptions/Create.cshtml`) dùng jQuery để thêm bớt dòng nhập thuốc.
- **Phòng khám Online (Video Call):**
  - Thiết kế màn hình full-screen Gọi Video (`VideoCall/Room.cshtml`).
  - Xây dựng Popup Modal hiển thị sau khi kết thúc cuộc gọi để bác sĩ chốt Bệnh án nhanh (`_FinishCallModal.cshtml`).

---

## 3. NGUYỄN ĐỨC MINH (BACKEND DEVELOPER & SYSTEM ARCHITECT)
*Vai trò: Thiết kế Kiến trúc Cơ sở dữ liệu, Bảo mật, Phân quyền và Tích hợp các công nghệ, API nền tảng.*

**Công việc cụ thể đã thực hiện:**
- **Database & Identity (Nền tảng):**
  - Thiết kế toàn bộ Entity Models (`User`, `Doctor`, `Appointment`, `MedicalRecord`,...). Cấu hình `ApplicationDbContext` (Code-First Migration).
  - Cấu hình Identity để Phân quyền (Role-based: Admin, Doctor, Patient) và bảo mật mật khẩu.
  - Viết logic Khởi tạo dữ liệu mồi (`DbSeeder.cs`).
- **Công nghệ lõi & APIs (Services):**
  - Cấu hình WebSockets bằng **SignalR Hub** (`NotificationHub.cs`) để bắn thông báo realtime.
  - Tích hợp **Google Gemini API** (`GeminiService.cs`) dùng Prompt Engineering để làm lõi cho AI Chatbot.
  - Tích hợp **ZegoCloud SDK** (`ZegoTokenService.cs`) để cấp Token mã hóa AES-256 cho phòng Video Call.
- **Controllers Quản trị cốt lõi:**
  - `AdminController.cs`: Query dữ liệu thống kê ra Dashboard.
  - `AppointmentsController.cs`: Logic Duyệt/Hủy lịch, chuyển trạng thái.
  - `InvoicesController.cs`: Luồng tính tiền, sinh hóa đơn.
  - `VideoCallController.cs`: Luồng cấp quyền vào phòng.

---

## 4. NGUYỄN HUY HÙNG (BACKEND DEVELOPER)
*Vai trò: Phụ trách toàn bộ luồng Xử lý Nghiệp vụ Y tế thực tế, Quản lý Kho Dữ liệu và tự động gửi Email.*

**Công việc cụ thể đã thực hiện:**
- **Luồng Đặt Lịch & Bệnh Nhân (Home Controller):**
  - Viết logic bắt data đặt lịch (`BookAppointment`), kiểm tra chặn giờ trùng lặp (Conflict Check).
  - Query lịch sử khám bệnh cá nhân an toàn (`MyHistory`).
- **Luồng Khám Bệnh & Thuốc (Nghiệp vụ Y tế lõi):**
  - `MedicalRecordsController.cs`: Logic lưu trữ hồ sơ bệnh án.
  - `PrescriptionsController.cs`: Viết vòng lặp lưu danh sách Đơn thuốc, kết hợp **Transaction & ExecutionStrategy** để bắt lỗi Hết hàng/Trừ tồn kho thuốc an toàn tuyệt đối.
- **Quản lý Master Data (CRUD):**
  - Xử lý thêm/sửa/xóa hệ thống danh mục: Bác sĩ (`DoctorsController`), Bệnh nhân (`PatientsController`), Chuyên khoa (`SpecialtiesController`), Thuốc (`MedicinesController`).
  - Viết API phân ca làm việc cho bác sĩ (`SchedulesController`).
- **Tính năng Hỗ trợ:**
  - Xử lý việc gửi thư Xác nhận đặt lịch (`EmailService.cs`).
  - Quản lý dữ liệu Đặt Dịch vụ Mở rộng (`ServicesController` lo gói khám, vaccine, y tế tại nhà).
  - Viết logic đẩy lịch sử chat lên AI (`ChatController.cs`).
