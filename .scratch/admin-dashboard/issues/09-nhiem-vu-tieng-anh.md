# 09 — Quản trị Nhiệm vụ tiếng Anh và quyền riêng tư hội thoại

**Nội dung cần xây dựng:** Cho Admin theo dõi Nhiệm vụ tiếng Anh ở mức tổng hợp và chỉ mở hội thoại theo một vụ việc có lý do, đồng thời thực thi thời hạn lưu dữ liệu đã thống nhất.

**Bị chặn bởi:** 03 — Tạo Bản ghi kiểm toán quản trị bất biến; 06 — Tra cứu Hồ sơ học tập ở chế độ chỉ đọc.

**Trạng thái:** `ready-for-agent`

- [ ] Danh sách nhiệm vụ chỉ hiển thị thông tin khái quát, hỗ trợ lọc, sắp xếp và phân trang phía máy chủ.
- [ ] Mở hội thoại yêu cầu loại vụ việc, mã tham chiếu nếu có và lý do; dữ liệu chỉ được trả sau khi audit thành công.
- [ ] Phản hồi dành cho Admin loại bỏ câu lệnh hệ thống, khóa bí mật và chi tiết vận hành nội bộ của Nhà cung cấp AI.
- [ ] Không có tìm kiếm toàn văn trong hội thoại và không có cập nhật định kỳ hội thoại bằng AJAX.
- [ ] Nội dung chi tiết hết hạn sau 90 ngày; kết quả, trạng thái, số lượt và số liệu tổng hợp vẫn được giữ.
- [ ] Vụ việc đang mở có thể tạm giữ nội dung đến khi kết thúc nhưng không quá 12 tháng.
- [ ] Tác vụ dọn dữ liệu chạy theo lô giới hạn, có thể chạy lặp an toàn và không ghi toàn bộ nội dung bị xóa vào log.
- [ ] Kiểm thử với thời gian giả bao phủ mốc 90 ngày, vụ việc đang mở, trần 12 tháng và dữ liệu tổng hợp còn lại.
- [ ] Kiểm thử HTTP chứng minh người không đủ quyền hoặc không nhập lý do không nhận được hội thoại.

