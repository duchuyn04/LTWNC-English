# Giải thích các mẫu thiết kế trong project

Các tài liệu trong thư mục này dành cho người mới học lập trình. Mỗi tài liệu đi theo cùng một thứ tự:

1. Giải thích mẫu bằng ví dụ đời thường.
2. Mô tả code trước khi áp dụng mẫu.
3. Giải thích vì sao chức năng phù hợp với mẫu.
4. Chỉ ra các class thật trong project.
5. Đi qua luồng chạy và một vài đoạn code ngắn.

## Danh sách theo nhóm GoF

### Mẫu khởi tạo (2)

| Mẫu | Chức năng áp dụng | Tài liệu |
| --- | --- | --- |
| Prototype | Sao chép bộ thẻ công khai hoặc nhân bản bộ thẻ của owner | [Đọc Prototype](prototype.md) |
| Factory Method | Tạo đúng Command từ loại thao tác | [Đọc Factory Method](factory.md) |

### Mẫu cấu trúc (2)

| Mẫu | Chức năng áp dụng | Tài liệu |
| --- | --- | --- |
| Adapter | Chuyển đổi giữa contract ứng dụng và API OpenAI-compatible | [Đọc Adapter](adapter.md) |
| Decorator | Bổ sung cache cho thư viện công khai | [Đọc Decorator](decorator.md) |

### Mẫu hành vi (6)

| Mẫu | Chức năng áp dụng | Tài liệu |
| --- | --- | --- |
| Strategy | Chọn cách lấy thẻ và hiển thị từng chế độ học | [Đọc Strategy](strategy.md) |
| State | Xử lý lịch ôn theo giai đoạn ghi nhớ | [Đọc State](state.md) |
| Chain of Responsibility | Fallback lần lượt qua các provider AI | [Đọc Chain of Responsibility](chain-of-responsibility.md) |
| Command | Đóng gói thao tác hàng loạt trên thẻ | [Đọc Command](command.md) |
| Memento | Giữ trạng thái cũ để hoàn tác thao tác trên thẻ | [Đọc Memento](memento.md) |
| Observer | Phát sự kiện học cho thành tích và logging | [Đọc Observer](observer.md) |

Tổng cộng: **2 mẫu khởi tạo, 2 mẫu cấu trúc và 6 mẫu hành vi**.

## Thứ tự đọc gợi ý

Đọc `Prototype` trước vì mẫu này ít thành phần nhất. Sau đó đọc `Factory Method`, `Adapter`, `Decorator`, `Strategy`, `State`, `Command`, `Memento`, `Observer` và `Chain of Responsibility`. Khi đọc phần AI, nên xem `Adapter` trước `Chain of Responsibility` vì chain sử dụng adapter làm receiver.
