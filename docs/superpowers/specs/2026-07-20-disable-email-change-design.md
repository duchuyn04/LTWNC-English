# Khóa chức năng đổi email

## Mục tiêu

Người dùng đã đăng ký không thể đổi địa chỉ email đăng nhập từ trang hồ sơ hoặc bằng cách gửi request trực tiếp tới ứng dụng. Email hiện tại vẫn được hiển thị ở chế độ chỉ đọc.

## Phạm vi

- Gỡ form đổi email khỏi trang chỉnh sửa hồ sơ.
- Gỡ action POST `/Account/Profile/ChangeEmail` khỏi `ProfileController`.
- Gỡ `ChangeEmailAsync` khỏi `IProfileService` và `ProfileService`.
- Xóa `ChangeEmailViewModel` vì không còn consumer.
- Cập nhật test để xác nhận giao diện không cung cấp chức năng đổi email và API nội bộ không còn luồng thay đổi email.

Luồng đăng ký, đăng nhập, đổi mật khẩu, cập nhật username/profile và các thao tác quản trị tài khoản không thay đổi.

## Hành vi mong muốn

1. Trang `/Account/Profile/Edit` hiển thị email hiện tại nhưng không render input chỉnh sửa, form hoặc nút gửi yêu cầu đổi email.
2. Request POST tới endpoint cũ `/Account/Profile/ChangeEmail` không khớp action nào và nhận `404 Not Found`.
3. Không còn phương thức nghiệp vụ nào trong `IProfileService` có thể đổi email.
4. Email trong `AspNetUsers` không bị thay đổi bởi thao tác chỉnh sửa profile.

## Thiết kế triển khai

Thay đổi được thực hiện ở cả presentation và server boundary. View chỉ render giá trị email dạng chỉ đọc. Controller không còn endpoint đổi email, và service không còn API đổi email. Cách này loại bỏ cả đường thao tác thông thường lẫn request thủ công, đồng thời tránh giữ mã chết trả lỗi cho một chức năng đã bị khóa vĩnh viễn.

Không cần migration database vì schema không thay đổi.

## Kiểm thử

- Sửa markup test để yêu cầu trang chỉnh sửa không chứa `ChangeEmail`, `NewEmail` hoặc form đổi email, nhưng vẫn hiển thị email hiện tại.
- Thêm integration test gửi POST tới endpoint cũ và xác nhận `404 Not Found`, nếu hạ tầng integration hiện có hỗ trợ tuyến profile.
- Chạy test liên quan đến profile trước, sau đó chạy toàn bộ `dotnet test` và `dotnet build`.

## Tiêu chí hoàn thành

- Không còn route, service method, view model hoặc form đổi email.
- Email hiện tại chỉ được hiển thị, không thể chỉnh sửa.
- Request tới endpoint cũ không làm thay đổi dữ liệu và trả `404`.
- Toàn bộ test và build thành công.
