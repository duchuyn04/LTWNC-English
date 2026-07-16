# Thiết kế: cải thiện UI trình chỉnh sửa bộ thẻ

## Mục tiêu

Làm cho trang quản lý bộ thẻ dễ sử dụng khi có nhiều thẻ: danh sách không kéo dài toàn trang, khu vực chi tiết và form thêm mới tách biệt, có điều hướng nhanh và thao tác đánh dấu sao không cần reload.

## Phạm vi

- Chỉ thay đổi trang `Views/FlashcardSet/Edit.cshtml`, CSS/JS liên quan và endpoint toggle sao phục vụ trang này.
- Không thay đổi mô hình dữ liệu, cách import file, CRUD nghiệp vụ hoặc các Study Mode.
- Giữ nguyên checkbox chọn nhiều thẻ cho thao tác Delete/Star/Unstar hàng loạt; checkbox này là lựa chọn hàng loạt, không đại diện cho trạng thái sao của thẻ.

## Bố cục mới

Trang Edit được chia thành ba lớp:

1. Sidebar điều hướng nhỏ ở bên trái.
2. Khu vực nội dung chính gồm thông tin bộ thẻ, import file và trình chỉnh sửa thẻ.
3. Trong trình chỉnh sửa thẻ, danh sách và chi tiết là hai panel độc lập; form “Thêm từ mới” là một card riêng bên dưới, không nằm trong panel chi tiết của thẻ đang chọn.

Sidebar có bốn anchor cố định:

- `Thông tin bộ thẻ` → card chỉnh title/description/public.
- `Nhập từ file` → card upload CSV/XLSX.
- `Danh sách từ` → panel danh sách và batch toolbar.
- `Thêm từ mới` → form tạo thẻ mới.

Các anchor dùng `scroll-behavior: smooth`; không tạo route mới. Trên màn hình nhỏ sidebar chuyển thành hàng ngang có thể cuộn, còn nội dung chuyển thành một cột.

Nút “Thêm” trong header của danh sách bị loại bỏ vì đã có anchor “Thêm từ mới” và form riêng.

## Fixed split editor

`.vocab-editor` giữ layout hai cột trên desktop. Panel danh sách có chiều cao cố định theo viewport, giới hạn ở `min(70vh, 720px)` và `overflow-y: auto`; số lượng thẻ tăng không làm trang kéo dài vô hạn. Panel chi tiết có cùng giới hạn chiều cao và scroll riêng khi nội dung một thẻ dài hơn vùng nhìn. Hai panel căn trên cùng (`align-items: start`).

Mỗi thẻ trong danh sách vẫn có nút chọn để mở panel chi tiết tương ứng. Chỉ panel có `is-active` được hiển thị; panel chi tiết không chứa form thêm mới. Form thêm mới được đặt sau `.vocab-editor` trong một section có `id="add-card-form"`, card header và spacing riêng.

## Đánh dấu sao không reload

Trạng thái sao trên form chi tiết, form thêm mới và item trong danh sách dùng checkbox semantic nhưng được tạo hình ngôi sao, không hiển thị ô vuông mặc định. Control có:

- `input type="checkbox"` ẩn phần native nhưng giữ trạng thái checked, kết hợp với `label`/button hình ngôi sao; có `aria-label` mô tả hành động và focus style rõ ràng.
- class `is-starred` khi đang bật; màu và nền thay đổi bằng CSS.
- `data-toggle-star-url`, `data-card-id` và `data-star-target` để JavaScript cập nhật đúng card.

Khi bấm sao trên một thẻ đã tồn tại, JavaScript gửi `POST` tới endpoint JSON mới của `FlashcardSetController`, kèm antiforgery token và `setId/cardId`. Service hiện có `ToggleStarAsync` được dùng lại; controller trả `{ success, isStarred }`. Khi thành công, UI cập nhật icon, class, `checked`/`aria-checked` và sao trong item danh sách ngay lập tức. Khi lỗi, UI khôi phục trạng thái trước đó và hiển thị thông báo ngắn; không reload trang.

Form thêm mới giữ field hidden/giá trị sao tương thích với action `AddCard`; nút sao chỉ cập nhật hidden input trước khi submit.

Checkbox vuông trong batch toolbar vẫn giữ nguyên vì nó biểu thị lựa chọn nhiều card cho Command hiện có, không phải trạng thái starred. Giao diện phải đặt label/tooltip khác biệt để người dùng không nhầm hai loại checkbox.

## Input và responsive

Các input văn bản trong `.vocab-grid` dùng `width: 100%`, `min-width: 0`, `box-sizing: border-box`; grid không đặt chiều rộng cứng theo nội dung. Trường `backText` và `exampleMeaning` chuyển thành `textarea` auto-grow: chiều cao bắt đầu ở khoảng ba dòng, tăng theo nội dung khi người dùng nhập, và có `max-height`/scroll nội bộ để một giá trị cực dài không phá layout. Các input một dòng còn lại vẫn giãn theo chiều rộng cột. Ở breakpoint hiện có, grid chuyển thành một cột và panels xếp dọc; mỗi panel vẫn có giới hạn chiều cao hợp lý.

## Data flow và kiến trúc

- Controller chỉ nhận request, kiểm tra user, gọi `IFlashcardSetService.ToggleStarAsync`, trả JSON hoặc lỗi HTTP; không chứa EF logic.
- Service hiện có tiếp tục là nơi kiểm tra owner và cập nhật `IsStarred`.
- View chỉ render state ban đầu và data attributes; JavaScript chịu trách nhiệm optimistic UI có rollback khi request thất bại.
- Không thêm thư viện frontend mới; dùng JavaScript hiện có và CSS riêng trong `edit.css`.

## Xử lý lỗi và accessibility

- Nếu user chưa đăng nhập, endpoint trả Challenge như các action hiện tại.
- Nếu card không tồn tại hoặc không thuộc set/user, endpoint trả `404`/`403` phù hợp; JavaScript không đổi state cuối cùng.
- Nút sao có focus style, `aria-checked`, `aria-label` và không phụ thuộc chỉ vào màu để biểu thị bật/tắt.
- Anchor sidebar có focus-visible; thao tác bằng bàn phím vẫn mở panel và toggle sao được.
- Không dùng inline style mới cho layout; các style responsive đặt trong `edit.css`.

## Kiểm thử

- View test/HTML inspection xác nhận có đủ bốn anchor, không còn nút “Thêm” trong header danh sách, form thêm mới nằm ngoài `.vocab-detail`, danh sách có class fixed-scroll.
- Controller test cho endpoint toggle sao: owner thành công trả JSON; unauthenticated Challenge; card/set không hợp lệ trả lỗi; service được gọi đúng.
- JavaScript smoke test/manual: click sao cập nhật cả detail/list không reload; lỗi request rollback state; submit form thêm mới truyền đúng hidden star value.
- Responsive/manual: 20+ thẻ chỉ tạo scroll trong danh sách; panel detail không bị kéo dài theo danh sách; màn hình nhỏ sidebar và panels xếp đúng.
- Chạy toàn bộ `dotnet test` và `git diff --check` trước khi bàn giao.

## Ngoài phạm vi

- Không thay đổi batch Command hoặc thêm thao tác undo mới.
- Không thay đổi cấu trúc database hay API import CSV/XLSX.
- Không xây dựng modal/tabs thay cho split editor.
