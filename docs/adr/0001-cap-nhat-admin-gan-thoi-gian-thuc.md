# ADR 0001: Cập nhật gần thời gian thực bằng AJAX

## Trạng thái

Đã chấp thuận

## Bối cảnh

Bảng điều khiển quản trị cần dữ liệu vận hành mới về trạng thái Nhà cung cấp AI, tỷ lệ lỗi AI, số Báo cáo nội dung đang chờ, cảnh báo và các chỉ số tổng quan. Ứng dụng hiện là hệ thống ASP.NET Core MVC sử dụng Razor Views; phiên bản 1 chưa có nhu cầu khác về kết nối hai chiều liên tục hoặc một ứng dụng giao diện tách biệt.

## Quyết định

Giữ giao diện quản trị trong Area `Admin` của ASP.NET Core và sử dụng Razor Views. Các khối dữ liệu vận hành được làm mới bằng yêu cầu AJAX có xác thực mỗi 30 giây.

Việc gọi định kỳ tạm dừng khi thẻ trình duyệt bị ẩn và làm mới ngay khi thẻ hoạt động trở lại. Danh sách người dùng, hồ sơ học tập và hội thoại Nhiệm vụ tiếng Anh không được gọi định kỳ. Các yêu cầu thay đổi dữ liệu vẫn phải kiểm tra mã chống giả mạo yêu cầu và quyền Admin.

## Hệ quả

- Các khối vận hành cập nhật mà không phải tải lại toàn bộ trang.
- Ứng dụng giữ nguyên mô hình triển khai và kết xuất MVC hiện có.
- Máy chủ nhận lượng yêu cầu định kỳ có giới hạn khi bảng điều khiển Admin đang mở.
- Dữ liệu có thể chậm khoảng 30 giây và không phải luồng sự kiện tức thời.
- Có thể xem xét SignalR trong tương lai nếu cần đẩy dữ liệu tức thời, cập nhật tần suất cao hoặc theo dõi đồng thời nhiều sự kiện.

## Các phương án đã cân nhắc

- **Tải lại toàn bộ trang:** đơn giản hơn nhưng làm gián đoạn quá trình điều tra và mất trạng thái bộ lọc.
- **SignalR:** độ trễ thấp hơn nhưng làm tăng độ phức tạp về vòng đời kết nối, mở rộng hệ thống và triển khai mà phiên bản 1 chưa cần.
- **Giao diện một trang tách biệt:** quản lý trạng thái phía máy khách phong phú hơn nhưng tạo thêm kiến trúc định tuyến và giao diện chưa mang lại đủ lợi ích hiện tại.
