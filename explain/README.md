# Giải thích các mẫu thiết kế trong project

Các tài liệu trong thư mục này dành cho người mới học lập trình. Mỗi tài liệu đi theo cùng một thứ tự:

1. Giải thích mẫu bằng ví dụ đời thường.
2. Mô tả code trước khi áp dụng mẫu.
3. Giải thích vì sao chức năng phù hợp với mẫu.
4. Chỉ ra các class thật trong project.
5. Đi qua luồng chạy và một vài đoạn code ngắn.

## Danh sách

| Mẫu | Chức năng áp dụng | Tài liệu |
| --- | --- | --- |
| Prototype | Sao chép bộ thẻ công khai vào thư viện cá nhân | [Đọc Prototype](prototype.md) |
| Strategy | Chọn cách lấy thẻ và hiển thị từng chế độ học | [Đọc Strategy](strategy.md) |
| Command | Đóng gói thao tác hàng loạt trên thẻ | [Đọc Command](command.md) |
| Memento | Giữ trạng thái cũ để hoàn tác thao tác trên thẻ | [Đọc Memento](memento.md) |
| Factory Method | Tạo đúng Command từ loại thao tác | [Đọc Factory Method](factory.md) |
| Observer | Phát sự kiện học cho thành tích và logging | [Đọc Observer](observer.md) |
| State | Xử lý lịch ôn theo giai đoạn ghi nhớ | [Đọc State](state.md) |
| Chain of Responsibility | Fallback lần lượt qua các provider AI | [Đọc Chain of Responsibility](chain-of-responsibility.md) |
| Adapter | Chuyển đổi giữa contract ứng dụng và API OpenAI | [Đọc Adapter](adapter.md) |
| Decorator | Bổ sung cache cho thư viện công khai | [Đọc Decorator](decorator.md) |

## Thứ tự đọc gợi ý

Đọc `Prototype` trước vì mẫu này ít thành phần nhất. Sau đó đọc `Strategy`, `Command`, `Memento` và `Factory Method`. Các tài liệu còn lại là `Observer`, `State`, `Chain of Responsibility`, `Adapter` và `Decorator`; chúng liên quan nhiều object phối hợp với nhau hơn.
