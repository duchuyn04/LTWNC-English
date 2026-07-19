# 08 — Cách ly và khôi phục bộ flashcard

**Nội dung cần xây dựng:** Hoàn thiện quy trình kiểm duyệt bằng Cách ly nội dung và khôi phục, đồng thời bảo vệ nội dung riêng tư khi Admin cần hỗ trợ.

**Bị chặn bởi:** 07 — Tiếp nhận và xử lý Báo cáo nội dung.

**Trạng thái:** `completed`

- [x] Bộ flashcard có trạng thái kiểm duyệt, lý do công khai, ghi chú nội bộ tùy chọn, người/thời điểm xử lý và khóa phiên bản.
- [x] Admin có thể cách ly từ báo cáo đang chờ hoặc từ trang chi tiết, bắt buộc xác nhận và lý do.
- [x] Bộ bị cách ly biến mất khỏi tìm kiếm, chia sẻ, sao chép và học công khai nhưng lịch sử học cũ không bị xóa.
- [x] Tác giả vẫn xem/sửa được bộ, thấy trạng thái/lý do công khai/thời điểm nhưng không thể tự xuất bản lại.
- [x] Chỉ Admin được khôi phục; cách ly và khôi phục đều có audit và phát hiện xung đột đồng thời.
- [x] Danh sách Admin chỉ hiển thị thông tin khái quát của bộ riêng tư; mở nội dung chi tiết yêu cầu lý do và audit.
- [x] Ghi chú nội bộ và bằng chứng kiểm duyệt không xuất hiện ở giao diện tác giả.
- [x] Không thêm thao tác Admin sửa nội dung hoặc xóa cứng bộ.
- [x] Kiểm thử bao phủ mọi truy vấn công khai hiện có để chứng minh bộ cách ly không bị lộ.

## Bình luận

2026-07-19: Hoàn thành issue 08. Đã thêm trạng thái kiểm duyệt và khóa phiên bản cho FlashcardSet, luồng Admin /Admin/Content để cách ly/khôi phục, xử lý cách ly từ /Admin/ContentReports, cổng lý do/audit khi mở chi tiết bộ riêng tư, và chặn bộ bị cách ly khỏi tìm kiếm, chi tiết public, sao chép, báo cáo và học công khai. Tác giả vẫn xem/sửa được bộ và chỉ thấy lý do công khai. Đã chạy `dotnet test tests\ltwnc.Tests\ltwnc.Tests.csproj --filter AdminContentModerationTests` đạt 5/5, `--filter ContentReportTests` đạt 7/7 và full suite đạt 399/399.
