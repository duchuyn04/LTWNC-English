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
| Simple Factory | Tạo đúng Command từ loại thao tác | [Đọc Simple Factory](factory.md) |
| Observer | Phát sự kiện học cho thành tích và logging | [Đọc Observer](observer.md) |
| Adapter | Chuyển đổi giữa contract ứng dụng và API OpenAI | [Đọc Adapter](adapter.md) |
| Protection Proxy | Kiểm tra quyền trước khi xuất CSV Admin | [Đọc Protection Proxy](protection-proxy.md) |

## Thứ tự đọc gợi ý

Đọc `Prototype` trước vì mẫu này ít thành phần nhất. Sau đó đọc `Strategy`, `Command`, `Memento` và `Simple Factory`. Ba tài liệu cuối là `Observer`, `Adapter` và `Protection Proxy` vì chúng liên quan nhiều object phối hợp với nhau hơn.

`Simple Factory` trong project không phải Factory Method chuẩn GoF. Tài liệu riêng giải thích sự khác nhau để tránh học nhầm tên.
