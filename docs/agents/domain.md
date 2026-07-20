# Tài liệu miền

Project dùng một ngữ cảnh miền chung.

## Đọc trước khi khảo sát hoặc thay đổi code

- Đọc `CONTEXT.md` tại thư mục gốc.
- Đọc các ADR liên quan trong `docs/adr/`.
- Nếu file chưa tồn tại thì tiếp tục làm việc mà không tự tạo tài liệu không cần thiết.

## Cấu trúc

```text
/
├── CONTEXT.md
├── docs/adr/
└── mã nguồn ứng dụng
```

## Sử dụng đúng thuật ngữ

Tên khái niệm miền trong kế hoạch, hạng mục, kiểm thử và code phải dùng thuật ngữ đã định nghĩa trong `CONTEXT.md`. Không dùng các từ đồng nghĩa mà glossary yêu cầu tránh.

## Xử lý mâu thuẫn với ADR

Nếu một đề xuất mâu thuẫn với ADR hiện có, phải nêu rõ mâu thuẫn và lý do cần xem xét lại; không được âm thầm ghi đè quyết định trước đó.
