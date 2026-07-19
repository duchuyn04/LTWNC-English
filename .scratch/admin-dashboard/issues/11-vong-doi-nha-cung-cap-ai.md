# 11 — Đưa Nhà cung cấp AI vào Area với vòng đời an toàn

**Nội dung cần xây dựng:** Chuyển chức năng Nhà cung cấp AI hiện có vào Area Admin và hoàn thiện vòng đời tạo/cấu hình/kiểm tra/vô hiệu hóa mà không lộ khóa bí mật hoặc mất lịch sử.

**Bị chặn bởi:** 02 — Bảo vệ Phiên quản trị đặc quyền; 03 — Tạo Bản ghi kiểm toán quản trị bất biến.

**Trạng thái:** `ready-for-agent`

- [ ] Chức năng hiện có được chuyển vào Area và layout Admin; không còn controller/view sản xuất song song ngoài Area.
- [ ] Admin có thể tạo, sửa, thử kết nối, khám phá mô hình, chọn mô hình và đặt thứ tự ưu tiên.
- [ ] Khóa bí mật chỉ được nhập hoặc thay mới; giao diện/API không bao giờ trả khóa gốc và chỉ hiển thị trạng thái cùng bốn ký tự cuối.
- [ ] Thao tác xóa cứng được thay bằng vô hiệu hóa; nhà cung cấp đã có lịch sử không thể bị xóa.
- [ ] Một nhà cung cấp chính được chọn rõ ràng và việc thay nhà cung cấp chính yêu cầu Phiên quản trị đặc quyền còn mới hoặc xác nhận lại danh tính.
- [ ] Thay khóa, đổi nhà cung cấp chính, vô hiệu hóa và cấu hình quan trọng dùng POST, chống giả mạo, xác nhận, lý do, khóa phiên bản và audit.
- [ ] Việc kiểm tra tiếp tục áp dụng bảo vệ địa chỉ mạng riêng, giới hạn thời gian và xử lý lỗi an toàn hiện có.
- [ ] Thông tin Nhà cung cấp AI không xuất hiện trong giao diện hoặc phản hồi dành cho người học.
- [ ] Kiểm thử bao phủ migration đường dẫn, khóa chỉ ghi mới, xác nhận lại danh tính, vô hiệu hóa và audit.

