# 02 — Bảo vệ Phiên quản trị đặc quyền

**Nội dung cần xây dựng:** Bảo đảm chỉ Admin đã hoàn tất xác thực hai bước mới vào được khu vực quản trị, đồng thời cung cấp cơ chế xác nhận lại danh tính dùng chung cho những thao tác đặc biệt nhạy cảm.

**Bị chặn bởi:** 01 — Tạo khung Area Admin và giao diện phương án A.

**Trạng thái:** `completed`

- [x] Admin chưa bật xác thực hai bước được chuyển đến luồng thiết lập trước khi truy cập nội dung quản trị.
- [x] Admin đã bật xác thực hai bước nhưng chưa xác minh trong lần đăng nhập hiện tại phải hoàn tất bước xác minh.
- [x] Có luồng thiết lập, xác minh và mã khôi phục bằng tiếng Việt, không làm yếu các chính sách Identity hiện có.
- [x] Thời điểm xác thực gần nhất được ghi an toàn trong phiên và có chính sách yêu cầu xác nhận lại khi quá 15 phút.
- [x] Chính sách xác nhận lại có thể được các thao tác thay khóa AI hoặc đổi nhà cung cấp chính sử dụng mà không tự triển khai lại logic.
- [x] Sau đăng nhập, Admin về `/Admin`, người học về `/Set`; người đã đăng nhập không thể quay lại biểu mẫu đăng nhập, đăng ký hoặc trang chủ công khai.
- [x] Đường dẫn từ chối truy cập phân biệt đúng 403 với yêu cầu đăng nhập.
- [x] Kiểm thử HTTP bao phủ phiên chưa có hai bước, đã xác minh, hết thời hạn 15 phút và chuyển hướng theo vai trò.

## Bình luận

- 2026-07-19: Hoàn tất bảo vệ phiên quản trị đặc quyền. Đã xác minh bằng test HTTP cho setup/verify/recovery code, chuyển hướng theo vai trò, 403, phiên hết hạn 15 phút; chính sách xác nhận lại cũng được gắn cho thao tác thay đổi Nhà cung cấp AI nhạy cảm.
