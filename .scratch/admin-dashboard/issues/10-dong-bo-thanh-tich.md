# 10 — Theo dõi và Đồng bộ lại thành tích

**Nội dung cần xây dựng:** Cung cấp màn hình chỉ đọc về danh mục/kết quả thành tích và một thao tác Đồng bộ lại thành tích có kiểm soát để sửa dữ liệu lệch mà không tạo cơ chế cấp thưởng thủ công.

**Bị chặn bởi:** 03 — Tạo Bản ghi kiểm toán quản trị bất biến.

**Trạng thái:** `ready-for-agent`

- [ ] Admin xem được danh mục thành tích từ source code, số người đã nhận và kết quả theo người dùng.
- [ ] Không có biểu mẫu sửa định nghĩa, cấp hoặc thu hồi thành tích thủ công.
- [ ] Admin có thể chạy Đồng bộ lại thành tích cho một người dùng và, nếu an toàn, cho toàn hệ thống theo lô.
- [ ] Thao tác yêu cầu xác nhận, lý do, chống giả mạo và tạo audit với số lượng thay đổi/kết quả an toàn.
- [ ] Hệ thống ngăn chạy trùng trên cùng phạm vi và hiển thị trạng thái thành công/thất bại bằng tiếng Việt.
- [ ] Thất bại tạo nguồn dữ liệu để cảnh báo dashboard sử dụng nhưng không để lại trạng thái cấp dở dang.
- [ ] Việc tính toán tái sử dụng cơ chế thành tích hiện có và giữ tính duy nhất của mỗi mã thành tích theo người dùng.
- [ ] Kiểm thử tích hợp chứng minh đồng bộ khôi phục đúng dữ liệu, chạy lại an toàn và không hỗ trợ ghi đè thủ công.

