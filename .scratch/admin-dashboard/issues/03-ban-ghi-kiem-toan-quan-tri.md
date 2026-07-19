# 03 — Tạo Bản ghi kiểm toán quản trị bất biến

**Nội dung cần xây dựng:** Cung cấp một luồng hoàn chỉnh để ghi và tra cứu Bản ghi kiểm toán quản trị, làm cổng dùng chung cho mọi thao tác thay đổi và truy cập dữ liệu nhạy cảm trong các hạng mục sau.

**Bị chặn bởi:** 01 — Tạo khung Area Admin và giao diện phương án A.

**Trạng thái:** `completed`

- [x] Lược đồ lưu người thực hiện, hành động, loại/mã đối tượng, thời gian UTC, kết quả, lý do, mã tương quan và metadata an toàn.
- [x] API nội bộ chỉ cho ghi thêm; không cung cấp thao tác sửa hoặc xóa thủ công.
- [x] Metadata dùng danh sách trường cho phép và giới hạn kích thước; mật khẩu, khóa bí mật, câu lệnh AI và toàn bộ hội thoại bị loại bỏ.
- [x] Một sự kiện quản trị có thể quan sát được, như đăng nhập thành công vào Area, tạo bản ghi để chứng minh luồng đầu cuối hoạt động.
- [x] Admin có thể xem, tìm kiếm, lọc và phân trang nhật ký; người ngoài vai trò không truy cập được.
- [x] Cơ chế ghi thay đổi hỗ trợ dùng cùng giao dịch nghiệp vụ và thất bại an toàn nếu không thể tạo audit.
- [x] Truy cập dữ liệu nhạy cảm có thể yêu cầu audit thành công trước khi trả dữ liệu.
- [x] Kiểm thử tích hợp xác minh tính chỉ ghi thêm, phân quyền, lọc và không rò rỉ trường bí mật.

## Bình luận

- 2026-07-19: Hoàn thành. Thêm entity `AdminAuditLog` (migration `AddAdminAuditLogs`), dịch vụ `IAdminAuditService`/`AdminAuditService` trong `Services/Audit/` (chỉ `Enqueue`/`RecordAsync`/`SearchAsync`, không sửa/xóa; `Enqueue` để ghi cùng giao dịch nghiệp vụ, `RecordAsync` ném lỗi khi không ghi được để thất bại an toàn). Metadata qua `AdminAuditMetadata`: danh sách trường cho phép + chặn tuyệt đối khóa nhạy cảm (password/secret/apiKey/token/prompt/conversation/message), giới hạn 200 ký tự/giá trị và 2000 ký tự JSON. Ghi sự kiện `AdminArea.SignIn` khi Admin xác minh hai bước thành công (authenticator/recovery code) trong `AdminTwoFactorController`. Trang `/Admin/AuditLogs` xem/tìm kiếm/lọc/phân trang (mặc định 25, tối đa 100), hiển thị giờ Việt Nam qua `AdminTimeZone`. Lưu ý kỹ thuật: tham số lọc `action` phải dùng `[FromQuery(Name = "action")]` vì route value `action` của MVC chiếm ưu tiên model binding. Kiểm thử: 14 test dịch vụ (`AdminAuditServiceTests`) + 5 test tích hợp (`AdminAuditLogTests`); toàn bộ 372 test đạt.
