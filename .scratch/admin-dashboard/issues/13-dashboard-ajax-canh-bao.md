# 13 — Cập nhật dashboard và cảnh báo bằng AJAX

**Nội dung cần xây dựng:** Làm mới Ảnh chụp vận hành trên dashboard gần thời gian thực bằng AJAX và hiển thị các cảnh báo có thể hành động mà không tải lại trang hoặc theo dõi dữ liệu cá nhân.

**Bị chặn bởi:** 05 — Xây dashboard KPI theo khoảng thời gian; 07 — Tiếp nhận và xử lý Báo cáo nội dung; 10 — Theo dõi và Đồng bộ lại thành tích; 12 — Chuyển Nhà cung cấp AI dự phòng và ghi số liệu vận hành.

**Trạng thái:** `ready-for-agent`

- [ ] Điểm cuối chỉ đọc trả KPI, trạng thái Nhà cung cấp AI, tỷ lệ lỗi, số báo cáo chờ và cảnh báo bằng hợp đồng dữ liệu ổn định.
- [ ] Điểm cuối yêu cầu Admin, không cho cache công khai và không trả dữ liệu người dùng/hội thoại nhạy cảm.
- [ ] Trình duyệt làm mới mỗi 30 giây, không khởi động yêu cầu mới khi yêu cầu trước chưa hoàn tất.
- [ ] Việc gọi tạm dừng khi thẻ bị ẩn, làm mới ngay khi hoạt động lại và dừng khi rời trang.
- [ ] Cảnh báo bao phủ Nhà cung cấp AI chính không ổn định, tỷ lệ lỗi vượt ngưỡng, báo cáo chờ quá 24 giờ và đồng bộ thành tích thất bại.
- [ ] Cảnh báo được suy ra từ trạng thái hiện tại và tự biến mất khi nguyên nhân đã hết; không cần nút đóng thủ công.
- [ ] Lỗi mạng tạm thời không xóa dữ liệu đang hiển thị và có trạng thái thông báo dễ hiểu bằng tiếng Việt.
- [ ] Kiểm thử HTTP xác minh phân quyền/hợp đồng; kiểm thử trình duyệt xác minh chu kỳ 30 giây, ẩn/hiện thẻ và chống chồng yêu cầu.

