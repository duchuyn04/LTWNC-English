# 06 — Tra cứu Hồ sơ học tập ở chế độ chỉ đọc

**Nội dung cần xây dựng:** Cho Admin đi từ một người dùng đến danh sách và chi tiết phiên học nhằm hỗ trợ/điều tra lỗi, trong khi mọi dữ liệu học tập vẫn chỉ đọc và lần mở chi tiết được kiểm toán.

**Bị chặn bởi:** 03 — Tạo Bản ghi kiểm toán quản trị bất biến; 04 — Quản lý và bảo vệ tài khoản người dùng.

**Trạng thái:** `ready-for-agent`

- [ ] Admin có thể lọc, sắp xếp và phân trang phiên học theo người dùng, chế độ, trạng thái và thời gian.
- [ ] Trang chi tiết hiển thị dữ liệu phiên, câu trả lời, điểm và tiến độ phù hợp với từng chế độ học.
- [ ] Mở chi tiết cấp người học yêu cầu lý do hỗ trợ/điều tra và chỉ trả dữ liệu sau khi audit thành công.
- [ ] Không có điểm cuối hoặc điều khiển cho phép sửa điểm, sửa tiến độ hoặc xóa lịch sử.
- [ ] Thời gian hiển thị theo múi giờ Việt Nam, có ngày giờ đầy đủ và có thể bổ sung thời gian tương đối để đọc nhanh.
- [ ] Danh sách mặc định 25 và tối đa 100 dòng, không tải toàn bộ dữ liệu về trình duyệt.
- [ ] Kiểm thử HTTP xác minh phân quyền, lý do bắt buộc, audit, dữ liệu chỉ đọc và phân trang.

