# 05 — Xây dashboard KPI theo khoảng thời gian

**Nội dung cần xây dựng:** Biến trang tổng quan khung thành dashboard dữ liệu thật với sáu KPI, bộ lọc thời gian và so sánh kỳ trước để Admin theo dõi mức sử dụng hệ thống.

**Bị chặn bởi:** 01 — Tạo khung Area Admin và giao diện phương án A.

**Trạng thái:** `completed`

- [x] Hiển thị người dùng hoạt động, đăng ký mới, số phiên học, tỷ lệ hoàn thành, số Nhiệm vụ tiếng Anh và tỷ lệ lỗi AI.
- [x] Bộ lọc hỗ trợ 7/30/90 ngày, mặc định 30 ngày, giữ lựa chọn trên chuỗi truy vấn và so sánh với kỳ liền trước.
- [x] Người dùng hoạt động được tính một lần nếu có ít nhất một hoạt động học đã thống nhất trong khoảng chọn.
- [x] Tỷ lệ hoàn thành loại phiên còn hoạt động chưa quá 30 phút và tính phiên không hoạt động quá 30 phút là bỏ dở.
- [x] Ranh giới ngày được tính theo múi giờ Việt Nam nhưng truy vấn/lưu dữ liệu bằng UTC.
- [x] Chỉ số AI trả trạng thái “chưa đủ dữ liệu” khi chưa đủ mẫu thay vì hiển thị 0%.
- [x] Truy vấn dùng tổng hợp phía máy chủ, không tải toàn bộ bản ghi vào bộ nhớ và có chỉ mục phù hợp.
- [x] Kiểm thử dịch vụ bao phủ ranh giới thời gian, kỳ so sánh, người dùng trùng và các trường hợp không có dữ liệu.
- [x] Kiểm thử HTTP xác minh bộ lọc và nội dung hiển thị bằng dữ liệu quan sát được.

## Bình luận

- 2026-07-19: Hoàn tất dashboard KPI dữ liệu thật cho `/Admin` với bộ lọc 7/30/90 ngày, ranh giới ngày theo giờ Việt Nam, so sánh kỳ trước và log vận hành AI an toàn. Đã xác minh `dotnet build` và test KPI/HTTP liên quan; full suite còn 3 lỗi `AdminAuditLogTests` thuộc issue 03 chưa hoàn thành.
