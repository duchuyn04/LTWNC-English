# 15 — Xuất dữ liệu an toàn và dọn dữ liệu hết hạn

**Nội dung cần xây dựng:** Cho Admin xuất đúng hai loại dữ liệu được phép và vận hành thời hạn lưu Bản ghi kiểm toán quản trị một cách có giới hạn, quan sát được và an toàn khi chạy lặp.

**Bị chặn bởi:** 03 — Tạo Bản ghi kiểm toán quản trị bất biến; 09 — Quản trị Nhiệm vụ tiếng Anh và quyền riêng tư hội thoại; 13 — Cập nhật dashboard và cảnh báo bằng AJAX.

**Trạng thái:** `ready-for-agent`

- [ ] Admin có thể xuất CSV KPI theo bộ lọc thời gian hiện tại và Bản ghi kiểm toán theo bộ lọc hiện tại.
- [ ] Không có điểm xuất hàng loạt hồ sơ người dùng, lịch sử học, nội dung riêng tư hoặc hội thoại.
- [ ] CSV dùng mã hóa/tiêu đề phù hợp, thoát giá trị đúng chuẩn và vô hiệu hóa công thức nguy hiểm bắt đầu bằng ký tự đặc biệt.
- [ ] Khoảng thời gian và số dòng xuất bị giới hạn để tránh truy vấn hoặc tệp quá lớn.
- [ ] Mỗi lần xuất tạo audit gồm loại xuất, bộ lọc và số dòng, không ghi toàn bộ dữ liệu đã xuất.
- [ ] Tác vụ nền xóa Bản ghi kiểm toán quá 12 tháng theo lô, có thể chạy lặp và không cung cấp nút xóa thủ công.
- [ ] Lịch chạy/dọn dữ liệu có trạng thái vận hành đủ để phát hiện thất bại nhưng không tạo log chứa dữ liệu nhạy cảm.
- [ ] Kiểm thử với thời gian giả bao phủ mốc 12 tháng, chạy lại, giới hạn lô và dữ liệu chưa hết hạn.
- [ ] Kiểm thử HTTP xác minh quyền xuất, bộ lọc, chống công thức CSV và audit của chính thao tác xuất.

