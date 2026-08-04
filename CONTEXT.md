# LTWNC English

Thuật ngữ chung cho xác thực tài khoản và khôi phục quyền truy cập.

## Xác thực tài khoản

**Email đã xác thực**:
Địa chỉ email mà người dùng đã chứng minh quyền sở hữu bằng Google hoặc mã OTP email.

**Đăng nhập Google**:
Đăng nhập bằng tài khoản Google có email đã được Google xác thực; người dùng không phải nhập email thủ công.

**Liên kết tài khoản Google**:
Gắn một tài khoản Google vào tài khoản ứng dụng đã tồn tại. Việc liên kết email trùng tài khoản phải qua đăng nhập mật khẩu hoặc xác minh OTP trước, không tự động liên kết. Tài khoản Google cũng có thể tạo mật khẩu ứng dụng qua OTP email.

**Mã OTP email**:
Mã dùng một lần gửi tới email đã xác thực để chứng minh quyền sở hữu email trong các luồng bảo mật như đăng ký hoặc khôi phục mật khẩu. Mã gồm 6 chữ số, có hiệu lực 5 phút, bị vô hiệu ngay sau khi dùng thành công hoặc nhập sai 3 lần; chỉ được gửi lại sau 1 phút và tối đa 5 mã trong một giờ cho mỗi email.

**Tài khoản mật khẩu**:
Tài khoản ứng dụng đăng nhập bằng tên đăng nhập và mật khẩu; email được nhập khi đăng ký local và dùng để xác thực, khôi phục quyền truy cập. Tài khoản người dùng cũ được giữ nguyên đăng nhập và có thể dùng OTP gửi tới email đang lưu để khôi phục mật khẩu.

**Khôi phục mật khẩu**:
Người dùng nhập email đã đăng ký; hệ thống gửi mã OTP tới email đó để cho phép đặt mật khẩu mới. Sau khi đổi mật khẩu thành công, mọi phiên đăng nhập cũ của tài khoản bị thu hồi.

**Đăng ký local**:
Người dùng nhập email, username và mật khẩu. Hồ sơ đăng ký được giữ ở trạng thái chờ xác thực; chỉ sau khi OTP đúng mới tạo/kích hoạt tài khoản và cho phép đăng nhập tự động.

**Tài khoản Admin**:
Tài khoản được cấp riêng ngoài đăng ký công khai; form đăng ký local, đăng nhập Google và cơ chế cấp quyền công khai không được tạo hoặc nâng quyền Admin. Khôi phục mật khẩu Admin không thuộc luồng tự động này và được xử lý thủ công.

**Tên đăng nhập**:
Định danh người dùng dùng trong màn hình đăng nhập local, không thay thế email đã xác thực. Với tài khoản Google mới, tên đăng nhập khởi đầu là phần trước `@` của email; nếu trùng thì thêm hậu tố số tăng dần.
