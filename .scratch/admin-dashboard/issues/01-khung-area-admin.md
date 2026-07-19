# 01 — Tạo khung Area Admin và giao diện phương án A

**Nội dung cần xây dựng:** Tạo một đường đi hoàn chỉnh đến khu vực quản trị riêng, có định tuyến, chính sách vai trò, bố cục phương án A và nền kiểm thử HTTP để các hạng mục sau mở rộng mà không tạo thêm hệ thống giao diện hoặc cơ chế phân quyền song song.

**Bị chặn bởi:** Không — có thể bắt đầu ngay.

**Trạng thái:** `completed`

- [x] `/Admin` dùng tuyến Area và hiển thị trang tổng quan khung bằng layout riêng theo phương án A.
- [x] Chính sách vai trò Admin áp dụng cho toàn Area; khách được chuyển đến đăng nhập và người đã đăng nhập không có vai trò nhận trang 403.
- [x] Tuyến Area được đăng ký trước tuyến mặc định và không làm thay đổi các đường dẫn học tập hiện có.
- [x] Layout tái sử dụng token, kiểu chữ, màu sắc và thành phần của hệ thống thiết kế hiện có; không sao chép thành hệ thống thiết kế thứ hai.
- [x] Menu có đủ tám nhóm chức năng đã chốt và có liên kết mở giao diện người học trong thẻ mới.
- [x] Prototype phát triển không cung cấp dữ liệu giả hoặc bộ chọn biến thể trong giao diện sản xuất.
- [x] Có bộ máy kiểm thử tích hợp HTTP dùng ứng dụng thử nghiệm và SQLite trong bộ nhớ làm nền cho các hạng mục sau.
- [x] Kiểm thử chứng minh đúng hành vi của khách, người học và Admin, đồng thời các tuyến hiện có không bị hồi quy.
