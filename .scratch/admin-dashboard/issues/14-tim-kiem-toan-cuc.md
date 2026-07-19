# 14 — Tìm kiếm toàn cục an toàn

**Nội dung cần xây dựng:** Cho Admin tìm nhanh các đối tượng quản trị chính từ một ô tìm kiếm chung mà không lập chỉ mục nội dung nhạy cảm hoặc vượt qua quy tắc truy cập chi tiết.

**Bị chặn bởi:** 04 — Quản lý và bảo vệ tài khoản người dùng; 08 — Cách ly và khôi phục bộ flashcard; 09 — Quản trị Nhiệm vụ tiếng Anh và quyền riêng tư hội thoại.

**Trạng thái:** `ready-for-agent`

- [ ] Tìm theo người dùng, email, mã định danh, tiêu đề/mã bộ flashcard và mã Nhiệm vụ tiếng Anh.
- [ ] Kết quả chỉ trả loại đối tượng, thông tin nhận diện an toàn, trạng thái và liên kết đến trang quản trị tương ứng.
- [ ] Không lập chỉ mục hoặc tìm trong hội thoại, câu trả lời học tập hay nội dung bộ riêng tư.
- [ ] Mở kết quả nhạy cảm vẫn yêu cầu lý do và audit như khi điều hướng trực tiếp.
- [ ] Truy vấn giới hạn số kết quả theo từng loại, có phân trang hoặc “xem thêm” và không quét toàn bộ bảng ở bộ nhớ.
- [ ] Chuỗi tìm kiếm được chuẩn hóa, giới hạn độ dài và xử lý an toàn; không lộ việc tồn tại của dữ liệu mà Admin chưa hoàn tất bước truy cập nhạy cảm.
- [ ] Các trường tìm kiếm thường dùng có chỉ mục phù hợp và truy vấn không tạo vấn đề N+1.
- [ ] Kiểm thử HTTP bao phủ kết quả đa loại, không lộ nội dung riêng tư/hội thoại và giữ nguyên quy tắc audit.

