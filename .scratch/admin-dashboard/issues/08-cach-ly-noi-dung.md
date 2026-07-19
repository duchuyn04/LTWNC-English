# 08 — Cách ly và khôi phục bộ flashcard

**Nội dung cần xây dựng:** Hoàn thiện quy trình kiểm duyệt bằng Cách ly nội dung và khôi phục, đồng thời bảo vệ nội dung riêng tư khi Admin cần hỗ trợ.

**Bị chặn bởi:** 07 — Tiếp nhận và xử lý Báo cáo nội dung.

**Trạng thái:** `ready-for-agent`

- [ ] Bộ flashcard có trạng thái kiểm duyệt, lý do công khai, ghi chú nội bộ tùy chọn, người/thời điểm xử lý và khóa phiên bản.
- [ ] Admin có thể cách ly từ báo cáo đang chờ hoặc từ trang chi tiết, bắt buộc xác nhận và lý do.
- [ ] Bộ bị cách ly biến mất khỏi tìm kiếm, chia sẻ, sao chép và học công khai nhưng lịch sử học cũ không bị xóa.
- [ ] Tác giả vẫn xem/sửa được bộ, thấy trạng thái/lý do công khai/thời điểm nhưng không thể tự xuất bản lại.
- [ ] Chỉ Admin được khôi phục; cách ly và khôi phục đều có audit và phát hiện xung đột đồng thời.
- [ ] Danh sách Admin chỉ hiển thị thông tin khái quát của bộ riêng tư; mở nội dung chi tiết yêu cầu lý do và audit.
- [ ] Ghi chú nội bộ và bằng chứng kiểm duyệt không xuất hiện ở giao diện tác giả.
- [ ] Không thêm thao tác Admin sửa nội dung hoặc xóa cứng bộ.
- [ ] Kiểm thử bao phủ mọi truy vấn công khai hiện có để chứng minh bộ cách ly không bị lộ.
