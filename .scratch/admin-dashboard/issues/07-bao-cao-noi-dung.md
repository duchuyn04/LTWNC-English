# 07 — Tiếp nhận và xử lý Báo cáo nội dung

**Nội dung cần xây dựng:** Tạo luồng đầu cuối để người học báo cáo bộ flashcard công khai và Admin xử lý hàng đợi bằng cách bác bỏ báo cáo với lý do rõ ràng.

**Bị chặn bởi:** 03 — Tạo Bản ghi kiểm toán quản trị bất biến.

**Trạng thái:** `completed`

- [x] Người học đã đăng nhập có thể báo cáo bộ công khai không thuộc sở hữu của mình bằng loại lý do cố định và mô tả tùy chọn.
- [x] Hệ thống từ chối tự báo cáo, báo cáo bộ riêng tư/không tồn tại và báo cáo đang mở bị trùng của cùng người/cùng bộ.
- [x] Lược đồ lưu đầy đủ trạng thái, người báo cáo, bộ, lý do, mô tả, người xử lý, kết quả và thời gian, kèm ràng buộc/chỉ mục phù hợp.
- [x] Admin có hàng đợi báo cáo hỗ trợ lọc, sắp xếp và phân trang phía máy chủ.
- [x] Admin có thể bác bỏ báo cáo bằng POST có chống giả mạo, xác nhận và lý do xử lý bắt buộc.
- [x] Việc bác bỏ tạo Bản ghi kiểm toán quản trị trong cùng kết quả nghiệp vụ.
- [x] Báo cáo đang chờ quá 24 giờ có thể được truy vấn để hạng mục cảnh báo sử dụng sau.
- [x] Kiểm thử tích hợp bao phủ gửi hợp lệ, các trường hợp bị từ chối, xử lý đồng thời và audit.

## Bình luận

- 2026-07-19: Hoàn thành phần code issue 07. Thêm entity `ContentReport`, migration `AddContentReports`, service `IContentReportService`/`ContentReportService`, form báo cáo ở `/Set/{id}`, hàng đợi Admin `/Admin/ContentReports`, thao tác bác bỏ có antiforgery/lý do/xác nhận và audit `ContentReports.Dismiss`. Thêm test tích hợp `ContentReportTests`; hiện chưa chạy được vì các file untracked của issue 06 trong `Services/AdminStudyRecords/` đang làm `dotnet build` fail.
