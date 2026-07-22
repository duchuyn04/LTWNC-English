# Thiết kế: Layout đăng nhập/đăng ký production

Ngày: 2026-07-22
Trạng thái: Đã duyệt hướng trực quan qua prototype, chờ review spec

## 1. Bối cảnh và mục tiêu

Hai trang `Views/Account/Login.cshtml` và `Views/Account/Register.cshtml` hiện chỉ đặt form trong một card Bootstrap ở giữa layout chung. Giao diện hoạt động đúng nhưng chưa tạo được cảm giác của một bề mặt xác thực production: còn dùng navbar/footer của website, thiếu nhận diện riêng, không có hình ảnh học tập và chưa tối ưu mật độ theo chiều cao màn hình.

Mục tiêu của đợt refactor:

- Dùng thiết kế **Learning Studio** đã chọn ở prototype: khu hình ảnh bên trái, form bên phải.
- Giữ ngôn ngữ Warm Editorial hiện tại: nền giấy kem, forest ink, brass accent, `Newsreader` cho display và `Be Vietnam Pro` cho nội dung.
- Đăng nhập và đăng ký dùng chung một auth shell, không lặp cấu trúc layout.
- Trên desktop phổ biến có chiều cao từ 768 px trở lên, toàn bộ trang và form nằm trong một viewport, không bắt người dùng cuộn trong điều kiện font mặc định.
- Trên màn hình nhỏ, khi zoom hoặc khi validation tạo thêm nội dung, trang được phép cuộn tự nhiên; không cắt nội dung để cố giữ quy tắc một viewport.
- Giữ nguyên backend auth, endpoint, ViewModel, validation, antiforgery, lockout, remember-me và redirect hiện có.

## 2. Quyết định thiết kế đã chốt

Prototype đã thử ba hướng:

1. **Editorial split**: ảnh phủ nền và copy lớn. Hướng này giàu cảm xúc nhưng text dễ tranh vùng chủ thể trong ảnh.
2. **Immersive photo**: ảnh toàn màn hình và form kính nổi. Hướng này nổi bật nhưng độ tương phản và khả năng đọc phụ thuộc mạnh vào ảnh.
3. **Learning Studio**: form và khu học tập là hai vùng độc lập. Hướng này được chọn vì form rõ ràng, hình ảnh vẫn có vai trò thương hiệu và dễ kiểm soát responsive.

Phiên bản được duyệt là Learning Studio với các hiệu chỉnh:

- Desktop: **ảnh/studio bên trái, form bên phải**.
- Form và studio nằm trong một khung lớn có border, radius 24 px và shadow nhẹ.
- Layout compact theo chiều cao để không tạo scroll không cần thiết.
- Mobile/tablet: **form lên trước**, khu studio xuống sau hoặc được thu gọn; thứ tự DOM vẫn đặt form trước để hỗ trợ bàn phím và screen reader.

Prototype chỉ là nguồn tham chiếu trực quan. Code production phải được viết lại với validation, accessibility, asset local và tests; không promote nguyên code prototype.

## 3. Kiến trúc view

### 3.1. Auth layout dùng chung

Tạo `Views/Shared/_AuthLayout.cshtml` làm document shell riêng cho hai trang auth. Layout này chịu trách nhiệm:

- `<head>`, title, viewport, favicon, font và stylesheet auth.
- Header tối giản gồm logo LTWNC English và link “Về trang chủ”.
- Khung hai cột: khu Learning Studio bên trái và `@RenderBody()` bên phải.
- Khu Learning Studio dùng ảnh local, một flashcard minh họa, streak card và caption ngắn.
- Nạp script auth ở cuối trang và render section script của view để client validation vẫn hoạt động.

Không dùng navbar/footer của `_Layout.cshtml`. Auth là một task-focused surface; loại bỏ chrome chung giúp giảm phân tâm và tránh scroll do header/footer.

`Login.cshtml` và `Register.cshtml` đặt `Layout = "_AuthLayout"` và chỉ chứa nội dung/form riêng. Hai view không lặp image panel hoặc page shell.

### 3.2. CSS và JavaScript

Tạo:

- `wwwroot/css/auth.css`: toàn bộ style chỉ dành cho auth shell, có prefix `.auth-` để không rò sang trang khác.
- `wwwroot/js/auth.js`: progressive enhancement cho nút hiện/ẩn mật khẩu. Không chứa logic xác thực hoặc mutation ngoài form submit chuẩn.

Không đưa CSS auth vào `site.css`; auth layout có vòng đời và responsive rules riêng. Các token màu/type được khai báo bằng alias trỏ về cùng giá trị Warm Editorial để giao diện nhất quán mà không phụ thuộc DOM của layout chính.

## 4. Cấu trúc giao diện

### 4.1. Desktop

- Trang cao `100dvh`, có fallback `100vh`.
- Page padding nhỏ, chừa vùng an toàn cho focus ring và bóng của khung.
- Header cao gọn; khung auth dùng phần chiều cao còn lại qua flex/grid thay vì `min-height` cộng dồn.
- Grid 58/42: studio rộng hơn, form hẹp hơn.
- Studio dùng ảnh cover; flashcard và streak card là overlay trên ảnh nhưng không nằm sau nội dung form.
- Form căn giữa theo chiều dọc, có `max-width: 480px`.
- Media query theo chiều cao dưới 780 px giảm padding, gap, headline và chiều cao control; không dùng scale transform vì làm mờ chữ và sai focus geometry.

### 4.2. Tablet và mobile

- Từ 980 px trở xuống chuyển thành một cột.
- Form hiển thị trước trong DOM và trên màn hình.
- Khu studio nằm dưới form với chiều cao 280 px ở tablet và 220 px ở mobile; ẩn streak/caption, chỉ giữ ảnh và flashcard để tránh tăng chiều dài không cần thiết.
- Body cho phép cuộn tự nhiên.
- Từ 640 px trở xuống, trường đăng ký luôn là một cột, page card bỏ shadow lớn và giảm radius/padding.

### 4.3. Nội dung studio

Khu trái giữ tinh thần của variant C:

- Ảnh học tập có vùng chủ thể rõ, không đặt text quan trọng đè lên mặt/người.
- Flashcard mẫu với từ “curiosity”, IPA và nghĩa tiếng Việt.
- Streak card “12 ngày học liên tiếp”.
- Caption “Một tài khoản. Mọi tiến bộ đều được ghi lại.”

Các thành phần trên mang tính trang trí/marketing, không phải dữ liệu thật của user. Chúng phải được đánh dấu phù hợp (`aria-hidden="true"` hoặc `alt=""`) để screen reader không hiểu nhầm là trạng thái tài khoản.

## 5. Asset hình ảnh

Ảnh production phải là asset local trong `wwwroot/images/auth/`; không hotlink Unsplash như prototype. Tạo một ảnh photorealistic riêng cho dự án, có bố cục chủ thể phù hợp với flashcard overlay, sau đó xuất bản desktop WebP, mobile WebP và JPEG fallback.

Yêu cầu kỹ thuật:

- Tỉ lệ phù hợp vùng dọc/ngang của studio, ưu tiên ảnh có negative space và chủ thể không nằm ở vị trí overlay.
- Bản desktop tối thiểu 1600 × 1200 và không quá 300 KB; bản mobile crop ngang tối thiểu 960 × 480 và không quá 140 KB.
- Render bằng `<picture>`/`<img>` với `object-fit: cover`, khai báo `width`/`height` hoặc `aspect-ratio` để tránh layout shift.
- `<picture>` dùng `media`/`srcset` để desktop lấy bản lớn và viewport từ 980 px trở xuống chỉ lấy bản mobile; mobile không tải bản desktop.
- Vì ảnh nằm above-the-fold trên desktop, `<img>` dùng `loading="eager"` và `fetchpriority="high"`; mobile chỉ tải source nhỏ đã chọn bởi `<picture>`.
- Không phụ thuộc CDN ảnh bên thứ ba để auth vẫn hiển thị ổn khi mạng ngoài bị chặn.

## 6. Form và luồng dữ liệu

### 6.1. Đăng nhập

Giữ nguyên POST `/Account/Login` và `LoginViewModel`:

- Email.
- Mật khẩu.
- Ghi nhớ đăng nhập.
- Submit “Đăng nhập”.
- Link chuyển sang `/Account/Register`.

Không thêm “Quên mật khẩu” vì backend hiện không có password reset. UI không được tạo affordance dẫn đến luồng chưa tồn tại.

### 6.2. Đăng ký

Giữ nguyên POST `/Account/Register` và `RegisterViewModel`:

- Email.
- Tên đăng nhập.
- Mật khẩu.
- Xác nhận mật khẩu.
- Password hint: tối thiểu 8 ký tự, có chữ hoa, chữ thường và số.
- Submit “Tạo tài khoản”.
- Link chuyển sang `/Account/Login`.

Từ viewport 1200 px trở lên, email và username nằm hai cột để giảm chiều cao; password và confirm password vẫn nằm toàn hàng. Dưới 1200 px, tất cả trường là một cột.

### 6.3. Validation và trạng thái submit

- Giữ `asp-validation-summary="ModelOnly"` và `asp-validation-for` cho từng trường.
- Error message dùng vùng semantic phù hợp và màu đạt contrast; input lỗi có border/icon nhưng không chỉ dựa vào màu.
- Server-rendered validation là nguồn sự thật. jQuery validation chỉ tăng tốc phản hồi phía client.
- Nút hiện/ẩn mật khẩu đổi `type=password/text`, cập nhật label “Hiện/Ẩn” và `aria-label`; không thay đổi value.
- Không disable hoặc khóa nút submit bằng JavaScript trong phạm vi refactor này; browser submit chuẩn và server validation giữ nguyên.
- ModelState error làm form cao hơn thì desktop được phép cuộn trong page; không che/cắt lỗi để giữ một viewport.

Luồng dữ liệu không thay đổi:

1. Razor render ViewModel và ModelState.
2. User submit form có antiforgery token.
3. `AccountController` gọi `IAuthService` hiện tại.
4. Thành công redirect như hiện tại; thất bại render cùng view với lỗi.

## 7. Accessibility và tương tác

- Một `h1` duy nhất cho mỗi trang; label thật liên kết với input qua tag helper.
- Focus visible rõ trên link, input, checkbox và button.
- Thứ tự tab đi từ header tối giản đến form; không đi qua các decoration studio.
- Nút show password là `type="button"` và có vùng bấm tối thiểu phù hợp.
- Hỗ trợ `prefers-reduced-motion`; chỉ dùng transition ngắn cho hover/focus, không thêm entrance animation bắt buộc.
- Ở zoom 200% hoặc viewport thấp, page chuyển sang cuộn tự nhiên; không khóa `overflow` nếu nội dung không còn vừa.
- Contrast text/form đạt WCAG AA trên surface kem/forest.

## 8. Error handling và graceful degradation

- Nếu ảnh không tải, studio vẫn có forest/sage fallback background và form không thay đổi layout.
- Nếu JavaScript lỗi hoặc bị tắt, password giữ dạng ẩn và form submit/validation server vẫn hoạt động.
- Nếu font Google không tải, fallback Georgia/Segoe UI giữ typography đọc được và không làm control tràn.
- ModelState lỗi chung (sai email/mật khẩu, lockout, trùng tài khoản) hiển thị ở đầu form, không đặt trong image panel.

## 9. Phạm vi thay đổi dự kiến

Production implementation dự kiến chạm:

- `Views/Shared/_AuthLayout.cshtml` — mới.
- `Views/Account/Login.cshtml` — refactor markup, giữ binding.
- `Views/Account/Register.cshtml` — refactor markup, giữ binding.
- `wwwroot/css/auth.css` — mới.
- `wwwroot/js/auth.js` — mới.
- `wwwroot/images/auth/*` — asset local mới.
- `tests/ltwnc.Tests/Views/*Auth*Tests.cs` — markup/style/script contract tests.

Không đổi `LoginViewModel`, `RegisterViewModel`, `AccountController` production actions, `IAuthService`, database hoặc cookie configuration.

Sau khi implementation production được xác nhận, xóa prototype khỏi nhánh production:

- Action development-only `AccountController.AuthPrototype`.
- `Views/Account/AuthPrototype.cshtml`.
- `Views/Account/Prototypes/`.
- `wwwroot/css/auth-prototype.css`.
- `wwwroot/js/auth-prototype.js`.

## 10. Chiến lược kiểm thử

### 10.1. Contract/markup tests

- Login/Register dùng `_AuthLayout`.
- Form giữ đúng `asp-action`, method POST, field binding và validation spans.
- Login có remember-me; Register có username và confirm password.
- Auth layout dùng asset local, không còn URL ảnh bên thứ ba.
- Có link chuyển Login/Register và link về trang chủ.
- Password toggle dùng `type="button"` và có accessible label.
- Prototype route/action/assets không còn sau khi production implementation hoàn tất.

### 10.2. Integration/regression

- Các integration tests đăng nhập/đăng ký hiện có tiếp tục chạy không đổi.
- Kiểm tra login sai, lockout, login thành công, register validation và antiforgery không bị ảnh hưởng bởi markup mới.
- `dotnet build` và toàn bộ `dotnet test` phải pass.

### 10.3. Visual/responsive verification

Kiểm tra thủ công hoặc browser automation ở tối thiểu:

- 1440 × 900: login và register không scroll ở font mặc định.
- 1366 × 768: compact-height rules giữ form trong viewport khi chưa có validation error.
- 1024 × 768: breakpoint không làm form quá hẹp.
- 390 × 844: form lên trước, không overflow ngang, trang cuộn tự nhiên.
- Zoom 200%: mọi field và lỗi vẫn truy cập được.
- Ảnh bị chặn: fallback background vẫn có chất lượng chấp nhận được.

## 11. Tiêu chí hoàn thành

- Login và Register dùng chung auth shell production theo variant C đã duyệt.
- Desktop hiển thị studio trái/form phải và không có navbar/footer chung.
- Không scroll ở các desktop viewport mục tiêu khi chưa có validation error; short/mobile/zoom được cuộn an toàn.
- Ảnh được phục vụ local, tối ưu và không che nội dung form.
- Binding, validation, antiforgery và redirect auth giữ nguyên.
- Keyboard, focus, screen reader semantics và reduced motion được xử lý.
- Prototype được loại khỏi code production sau khi thiết kế thật hoàn tất.
- Build và test suite pass.
