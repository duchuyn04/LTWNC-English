# Domain Docs

Repo sử dụng mô hình single-context.

## Trước khi khám phá code

Đọc các tài liệu sau nếu chúng tồn tại:

- `CONTEXT.md` tại root
- `CONTEXT-MAP.md` tại root nếu sau này repo được tách thành nhiều context
- ADR liên quan trong `docs/adr/`

Nếu các file chưa tồn tại, tiếp tục làm việc mà không cảnh báo. Skill `/domain-modeling` sẽ tạo chúng khi một thuật ngữ hoặc quyết định thực sự cần được ghi lại.

## Sử dụng ngôn ngữ domain

Tên trong ticket, test, kế hoạch và code phải sử dụng thuật ngữ được định nghĩa trong `CONTEXT.md`. Không tự chuyển sang từ đồng nghĩa mà glossary yêu cầu tránh.

Nếu một thuật ngữ cần thiết chưa tồn tại, xem xét liệu đó là từ không thuộc domain hay một khoảng trống cần xử lý bằng `/domain-modeling`.

## Xung đột ADR

Nếu thay đổi mâu thuẫn với ADR hiện có, phải nêu rõ xung đột thay vì âm thầm ghi đè quyết định.
