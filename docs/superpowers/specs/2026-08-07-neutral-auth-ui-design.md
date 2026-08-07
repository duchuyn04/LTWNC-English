# Giao diện đăng nhập và đăng ký trung tính

## Mục tiêu

Giữ bố cục hiện tại của trang xác thực nhưng đổi câu chữ sang giọng trung tính, chuyên nghiệp. Hai phương thức đăng nhập phải có thứ bậc thị giác rõ ràng: biểu mẫu tài khoản là hành động chính, Google là lựa chọn phụ.

## Phạm vi

Thay đổi hai trang `Login` và `Register`, phần minh họa dùng chung trong `_AuthLayout`, cùng các kiểu liên quan trong `auth.css`. Không đổi luồng xác thực, model, validation, endpoint hoặc JavaScript.

## Nội dung

### Đăng nhập

- Nhãn đầu trang: `Tài khoản`
- Tiêu đề: `Đăng nhập`
- Mô tả: `Nhập thông tin tài khoản để tiếp tục.`
- Nút Google: `Đăng nhập bằng Google`, kèm logo Google dạng SVG inline
- Dòng phân cách: `Hoặc đăng nhập bằng tài khoản`
- Nút gửi biểu mẫu: `Đăng nhập`, không có ký hiệu mũi tên
- Tùy chọn ghi nhớ: `Duy trì đăng nhập trên thiết bị này`
- Liên kết đăng ký: `Chưa có tài khoản? Đăng ký`

### Đăng ký

- Nhãn đầu trang: `Tài khoản mới`
- Tiêu đề: `Tạo tài khoản`
- Mô tả: `Điền thông tin bên dưới. Mã xác thực sẽ được gửi đến email của bạn.`
- Nút Google: `Đăng ký bằng Google`, kèm logo Google dạng SVG inline
- Dòng phân cách: `Hoặc đăng ký bằng email`
- Nút gửi biểu mẫu: `Gửi mã xác thực`

### Phần minh họa dùng chung

- `Word of the day` đổi thành `Từ vựng hôm nay`.
- `Tap to reveal` đổi thành `Xem nghĩa`.
- Chú thích đổi thành `Lưu bộ thẻ và theo dõi tiến độ học tập.`
- Thông tin chuỗi ngày học được giữ nguyên vì đây là nội dung mô tả, không phải lời quảng cáo.

## Trình bày

- Giữ ảnh, khung biểu mẫu và bảng màu xanh, kem hiện tại.
- Bỏ thanh tiến trình trang trí trên `Login` và `Register` vì hai trang này không phải quy trình nhiều bước.
- Nút gửi biểu mẫu dùng nền xanh đậm và chữ trắng.
- Nút Google dùng nền trắng, viền xám và chữ xanh đậm.
- Dòng phân cách có hai đường kẻ mảnh để tách Google khỏi biểu mẫu.
- Trạng thái hover và focus phải giữ đủ tương phản, không đổi màu chữ ngoài ý muốn.

## Kiểm tra

- Viết kiểm tra nguồn view trước để xác nhận câu chữ mới, việc bỏ thanh tiến trình và class nút Google.
- Chạy toàn bộ test .NET và build dự án.
- Kiểm tra trực tiếp `Login` và `Register` ở kích thước desktop và mobile.
- Xác nhận trường nhập, validation, hiện mật khẩu và các liên kết vẫn hoạt động.

## Ngoài phạm vi

Không thiết kế lại các trang OTP, quên mật khẩu, đặt lại mật khẩu hoặc liên kết Google. Các trang này tiếp tục dùng thành phần và màu chung hiện có.
