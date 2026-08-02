# Thiết kế bổ sung tài liệu Decorator và State

## Mục tiêu

Bổ sung hai tài liệu còn thiếu trong `explain/` để tài liệu học khớp với 9 mẫu GoF được triển khai trong project.

## Phạm vi

- Tạo `explain/decorator.md` cho `CachedPublicLibraryServiceDecorator`.
- Tạo `explain/state.md` cho `ReviewStateMachine` và các concrete state.
- Cập nhật bảng danh sách và thứ tự đọc trong `explain/README.md`.
- Không thay đổi code ứng dụng hoặc hành vi runtime.

## Nội dung tài liệu

Mỗi tài liệu giữ cấu trúc đang dùng trong `explain/`: ý chính, ví dụ đời thường, cách làm trước khi áp dụng mẫu, lý do áp dụng, bảng vai trò GoF và class thật, luồng chạy, câu hỏi tự kiểm tra và kết luận.

- **Decorator:** giải thích `IPublicLibraryService` là Component, `PublicLibraryService` là Concrete Component, `CachedPublicLibraryServiceDecorator` là Decorator; nêu cache có điều kiện và chuyển tiếp truy vấn không cache được.
- **State:** giải thích `ReviewStateMachine` là Context, `IReviewState` là State, bốn state theo giai đoạn ôn tập là Concrete State; nêu việc khôi phục state từ database và transition sau khi rating.

## Tiêu chí hoàn thành

- Hai file mới tồn tại và link tương đối đúng từ `explain/README.md`.
- Danh sách tài liệu bao gồm đủ 9 mẫu GoF.
- Nội dung chỉ mô tả các class, luồng và giới hạn đang có trong code.
- `git diff --check` không báo lỗi whitespace.
