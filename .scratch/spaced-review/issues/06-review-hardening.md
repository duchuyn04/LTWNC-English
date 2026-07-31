# 06 — Hardening và tương thích dữ liệu Ôn tập đến hạn

**What to build:** Chức năng chịu được request trùng, retry, thay đổi thư viện và các thao tác quản lý bộ thẻ mà không làm sai quota, lịch ôn hoặc các chức năng cũ.

**Blocked by:** 02 — Lượt ôn nhiều thẻ đến hạn và tiếp tục; 03 — Đầy đủ bốn Giai đoạn ghi nhớ và preview khoảng hẹn; 04 — Hạn mức thẻ mới và Tạm dừng ôn tập theo bộ.

**Status:** resolved

- [x] Hai request bắt đầu gần nhau trả về cùng Lượt ôn đang hoạt động và không tạo quota trùng.
- [x] Hai request đánh giá cùng item chỉ chấp nhận đánh giá đầu tiên; request còn lại không tính lại transition.
- [x] Retry hoặc lỗi giữa transaction không tạo ReviewProgress/ReviewSessionItem dở dang làm sai trạng thái.
- [x] Xóa thẻ hoặc bộ thẻ dọn dữ liệu lịch ôn cần thiết và item không còn tồn tại được loại khỏi lượt mà không bù thẻ khác.
- [x] Clone bộ thẻ không kế thừa cấu hình hoặc Tiến độ ôn của nguồn.
- [x] Các test hiện có cho Flashcard, Quiz, Dictation, StudySession, UserProgress và Memento vẫn giữ kết quả cũ.

## Answer

- Làm idempotent cho Start/Rate, thêm optimistic concurrency trên `RatedAtUtc` và khôi phục kết quả đã lưu khi request retry.
- Dọn `ReviewProgress`/`ReviewSessionItem` khi xóa thẻ, giữ shell lịch sử đã hoàn tất và không bù item; clone giữ policy/progress mặc định độc lập.
- Tương thích provider InMemory/relational cho các luồng transaction và copy/delete.
- Đã xác minh: `dotnet build ltwnc.csproj --no-restore`, `dotnet test ... --no-restore` — 122/122 pass.
