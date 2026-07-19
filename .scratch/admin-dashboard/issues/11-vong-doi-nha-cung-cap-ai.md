# 11 — Đưa Nhà cung cấp AI vào Area với vòng đời an toàn

**Nội dung cần xây dựng:** Chuyển chức năng Nhà cung cấp AI hiện có vào Area Admin và hoàn thiện vòng đời tạo/cấu hình/kiểm tra/vô hiệu hóa mà không lộ khóa bí mật hoặc mất lịch sử.

**Bị chặn bởi:** 02 — Bảo vệ Phiên quản trị đặc quyền; 03 — Tạo Bản ghi kiểm toán quản trị bất biến.

**Trạng thái:** `completed`

- [x] Chức năng hiện có được chuyển vào Area và layout Admin; không còn controller/view sản xuất song song ngoài Area.
- [x] Admin có thể tạo, sửa, thử kết nối, khám phá mô hình, chọn mô hình và đặt thứ tự ưu tiên.
- [x] Khóa bí mật chỉ được nhập hoặc thay mới; giao diện/API không bao giờ trả khóa gốc và chỉ hiển thị trạng thái cùng bốn ký tự cuối.
- [x] Thao tác xóa cứng được thay bằng vô hiệu hóa; nhà cung cấp đã có lịch sử không thể bị xóa.
- [x] Một nhà cung cấp chính được chọn rõ ràng và việc thay nhà cung cấp chính yêu cầu Phiên quản trị đặc quyền còn mới hoặc xác nhận lại danh tính.
- [x] Thay khóa, đổi nhà cung cấp chính, vô hiệu hóa và cấu hình quan trọng dùng POST, chống giả mạo, xác nhận, lý do, khóa phiên bản và audit.
- [x] Việc kiểm tra tiếp tục áp dụng bảo vệ địa chỉ mạng riêng, giới hạn thời gian và xử lý lỗi an toàn hiện có.
- [x] Thông tin Nhà cung cấp AI không xuất hiện trong giao diện hoặc phản hồi dành cho người học.
- [x] Kiểm thử bao phủ migration đường dẫn, khóa chỉ ghi mới, xác nhận lại danh tính, vô hiệu hóa và audit.

## Bình luận

2026-07-19: Hoàn thành issue 11. Đã chuyển AI Providers vào Area Admin với layout Admin, bỏ controller/view sản xuất ngoài Area, thay xóa cứng bằng vô hiệu hóa có lý do/audit, thêm provider chính duy nhất bằng `IsPrimary`, khóa phiên bản `Version`, form cấu hình có lý do bắt buộc và API key chỉ ghi mới/thay thế/xóa chứ không trả khóa gốc. Router AI ưu tiên provider chính rồi fallback theo priority; test/discover giữ bảo vệ mạng riêng và xử lý lỗi an toàn hiện có. Đã chạy `dotnet test tests\ltwnc.Tests\ltwnc.Tests.csproj --filter AiProviderServiceTests` đạt 7/7, `--filter SecurityEndpointPolicyTests` đạt 13/13 và full suite đạt 402/402.
