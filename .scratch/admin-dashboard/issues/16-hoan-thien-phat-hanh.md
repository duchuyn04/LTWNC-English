# 16 — Hoàn thiện khả năng tiếp cận, hiệu năng và bảo mật phát hành

**Nội dung cần xây dựng:** Kiểm chứng toàn bộ khu vực quản trị như một bản phát hành hoàn chỉnh, sửa các khoảng trống tích hợp còn lại và bảo đảm giao diện, truy vấn, migration cùng chính sách bảo mật đáp ứng đặc tả.

**Bị chặn bởi:** 04 — Quản lý và bảo vệ tài khoản người dùng; 05 — Xây dashboard KPI theo khoảng thời gian; 06 — Tra cứu Hồ sơ học tập ở chế độ chỉ đọc; 07 — Tiếp nhận và xử lý Báo cáo nội dung; 08 — Cách ly và khôi phục bộ flashcard; 09 — Quản trị Nhiệm vụ tiếng Anh và quyền riêng tư hội thoại; 10 — Theo dõi và Đồng bộ lại thành tích; 11 — Đưa Nhà cung cấp AI vào Area với vòng đời an toàn; 12 — Chuyển Nhà cung cấp AI dự phòng và ghi số liệu vận hành; 13 — Cập nhật dashboard và cảnh báo bằng AJAX; 14 — Tìm kiếm toàn cục an toàn; 15 — Xuất dữ liệu an toàn và dọn dữ liệu hết hạn.

**Trạng thái:** `ready-for-agent`

- [ ] Toàn bộ chức năng dùng được bằng bàn phím, có focus rõ, nhãn/lỗi biểu mẫu đúng, thông báo trạng thái cho công nghệ hỗ trợ và dữ liệu chữ thay thế biểu đồ.
- [ ] Giao diện đầy đủ chức năng từ 360 px, tối ưu cho máy tính/máy tính bảng và tôn trọng tùy chọn giảm chuyển động.
- [ ] Mọi nội dung người dùng thấy là tiếng Việt; thời gian lưu UTC và hiển thị đúng múi giờ Việt Nam.
- [ ] Mọi danh sách mặc định 25, tối đa 100 dòng và truy vấn phía máy chủ; các truy vấn trọng yếu không có N+1 hoặc tải toàn bảng.
- [ ] Chỉ mục/ràng buộc quan trọng được kiểm tra qua migration và dữ liệu cũ nâng cấp với mặc định an toàn.
- [ ] Mọi yêu cầu ghi dùng phương thức, chống giả mạo, xác nhận, lý do, kiểm tra đồng thời và audit đúng đặc tả.
- [ ] Ma trận khách/người học/Admin/Admin chưa hai bước/Admin cần xác nhận lại được kiểm thử cho các tuyến nhạy cảm.
- [ ] Kiểm thử trình duyệt bao phủ AJAX, menu, hộp xác nhận, bàn phím và kích thước 360 px; kiểm thử chỉ dựa trên hành vi bên ngoài.
- [ ] Toàn bộ build, kiểm thử đơn vị, tích hợp, migration và trình duyệt đạt; không làm hồi quy luồng học tập hiện có.
- [ ] Prototype không xuất hiện như chức năng sản xuất và tài liệu bàn giao chỉ đúng đường dẫn Area chính thức.
