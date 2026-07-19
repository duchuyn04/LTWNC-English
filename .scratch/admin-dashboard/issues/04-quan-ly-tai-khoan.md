# 04 — Quản lý và bảo vệ tài khoản người dùng

**Nội dung cần xây dựng:** Cho Admin tìm và xử lý an toàn một tài khoản người dùng từ giao diện đến Identity, bao gồm khóa, mở khóa và thu hồi phiên, với xác nhận, lý do và audit đầy đủ.

**Bị chặn bởi:** 02 — Bảo vệ Phiên quản trị đặc quyền; 03 — Tạo Bản ghi kiểm toán quản trị bất biến.

**Trạng thái:** `completed`

- [x] Danh sách người dùng hỗ trợ tìm kiếm, lọc, sắp xếp và phân trang phía máy chủ, mặc định 25 và tối đa 100 dòng.
- [x] Trang chi tiết chỉ hiển thị hồ sơ/trạng thái cần thiết; không cho xem/đặt mật khẩu, sửa hồ sơ, xóa tài khoản hoặc đổi vai trò.
- [x] Khóa tài khoản có hiệu lực ngay, thu hồi mọi cookie hiện có và chặn đăng nhập mới cho đến khi Admin mở khóa.
- [x] Mở khóa giữ nguyên tiến độ, thành tích và nội dung của người dùng.
- [x] Lệnh thu hồi phiên hoạt động độc lập với Khóa tài khoản.
- [x] Hệ thống từ chối tự khóa, khóa Admin khởi tạo hoặc khóa làm số Admin hoạt động giảm về 0.
- [x] Mọi thao tác ghi dùng POST, mã chống giả mạo, hộp xác nhận, lý do bắt buộc, phát hiện xung đột và audit cùng kết quả.
- [x] Người bị khóa nhận thông báo chung và hướng dẫn liên hệ hỗ trợ, không thấy ghi chú nội bộ.
- [x] Kiểm thử HTTP bao phủ thành công, từ chối theo bất biến, cookie cũ mất hiệu lực và xung đột đồng thời.

## Bình luận

- 2026-07-19: Hoàn tất `/Admin/Users` gồm danh sách tìm kiếm/lọc/sắp xếp/phân trang server-side, trang chi tiết chỉ đọc, khóa/mở khóa/thu hồi phiên qua POST + antiforgery + confirm + lý do + concurrency stamp. Thao tác khóa dùng Identity lockout vô hạn và đổi security stamp; cookie validation từ chối tài khoản bị khóa ở request kế tiếp. Các bất biến tự khóa, Admin khởi tạo và Admin hoạt động cuối cùng đều bị từ chối và ghi audit `Denied`; thao tác thành công ghi audit `Success`. Kiểm thử: `AdminUserAccountTests` 7/7, nhóm Admin liên quan 32/32, full suite 379/379 đạt.
