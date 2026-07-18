# User Profile and Interactive 404 Design

**Date:** 2026-07-18

## Goal

Xây dựng hai module trong một delivery scope cho ứng dụng LTWNC:

1. User Profile công khai và riêng tư, có thống kê học tập, timeline cộng đồng, chỉnh sửa tài khoản và avatar crop.
2. Trang 404 sáng tạo bằng HTML, CSS và JavaScript, sau đó tích hợp cùng thiết kế vào ASP.NET Core MVC.

Hai module dùng chung visual language và tiêu chuẩn accessibility nhưng không chia sẻ service hoặc data model.

## Scope

### In scope

- Public profile `/u/{username}` theo layout Timeline cộng đồng.
- Trang chỉnh sửa riêng `/Account/Profile/Edit`.
- Bio, avatar, username, email, password và privacy controls.
- Avatar upload tối đa 5 MB và crop theo khung tròn.
- Statistics, streak, badges, activity timeline và public sets.
- Username đổi tối đa một lần mỗi 30 ngày.
- Prototype độc lập chạy bằng HTML, CSS và JavaScript thuần.
- Trang 404 toàn màn hình theo concept "Wrong Turn".
- Vocabulary card có mặt trước và mặt sau.
- User có thể lật card bằng nút hoặc phím `Space`.
- CTA rõ ràng để về trang chủ.
- Tích hợp với ASP.NET Core status-code middleware.
- Giữ đúng HTTP status `404`.
- Responsive cho desktop và mobile.
- Hỗ trợ keyboard, screen reader và reduced motion.

### Out of scope

- Follow, friend, social feed hoặc notification.
- Email verification vì app chưa cấu hình email sender.
- Bảng activity riêng; timeline được tổng hợp từ dữ liệu hiện có.
- Mini-game hoặc hệ thống điểm.
- Âm thanh hoặc phát âm tự động.
- Tracking và analytics cho lỗi 404.
- Thay đổi trang lỗi 500.
- Thư viện animation hoặc UI mới.
- Tìm kiếm route hoặc tự động đoán URL user muốn truy cập.

## Module Boundaries

### User Profile

Profile phụ thuộc ASP.NET Core Identity và các bảng dữ liệu học hiện có. Module này có entity, service, controller, view model, views và avatar storage riêng.

### Interactive 404

404 chỉ phụ thuộc MVC status-code middleware, một Razor view và static assets. Nó không truy cập dữ liệu Profile.

Hai module chỉ dùng chung typography, màu sắc và tiêu chuẩn keyboard/reduced-motion.

## User Profile Architecture

### Identity and profile data

`IdentityUser` tiếp tục lưu `UserName`, `Email`, `PasswordHash` và các trường authentication chuẩn.

Tạo bảng `UserProfiles` liên kết 1-1 với `AspNetUsers`:

```text
UserId                  PK/FK -> AspNetUsers.Id
Bio                     nullable, max 500 characters
AvatarPath              nullable
IsPublic                default true
ShowStats               default false
ShowBadges              default false
ShowActivity            default false
ShowPublicSets          default false
LastUsernameChangedAt   nullable
CreatedAt
UpdatedAt
```

Business entity hiện có tiếp tục dùng `UserId` kiểu `string`. Không dùng roles.

### Routes

```text
GET  /u/{username}
GET  /Account/Profile/Edit
POST /Account/Profile/Edit
POST /Account/Profile/Avatar
POST /Account/Profile/ChangeEmail
POST /Account/Profile/ChangePassword
```

`/u/{username}` là public profile. `/Account/Profile/Edit` và các POST action yêu cầu authentication.

### Services

`IProfileService` chịu trách nhiệm:

- Tìm public profile theo username.
- Tạo lazy `UserProfile` cho user cũ khi vào trang edit.
- Tổng hợp statistics, streak, badges, timeline và public sets.
- Áp dụng privacy controls trước khi query và trả dữ liệu.
- Cập nhật bio và privacy settings.
- Đổi username, email và password qua `UserManager<IdentityUser>`.
- Áp dụng username cooldown 30 ngày.

`IAvatarService` chịu trách nhiệm:

- Decode và validate ảnh thật ở server.
- Lưu ảnh crop với tên ngẫu nhiên.
- Thay avatar an toàn và xóa file cũ sau khi file mới đã lưu.
- Cleanup file mới nếu database update thất bại.

Controller chỉ xử lý authorization, model binding, gọi service và trả view/redirect.

## Public Profile Experience

Public profile dùng layout **Timeline cộng đồng**:

- Header có avatar, username và bio.
- Statistics summary ngắn.
- Timeline là nội dung chính.
- Badges và public sets hiển thị khi privacy toggle tương ứng bật.
- Nếu không có avatar, dùng chữ cái đầu username trong khung tròn.

Khi `IsPublic = false`, owner vẫn xem được profile đầy đủ; người khác thấy trang thông báo `Profile đang ở chế độ riêng tư` thay vì 404.

Email luôn ẩn khỏi public profile.

## Profile Statistics and Timeline

### Statistics

- Số bộ thẻ user sở hữu.
- Số bộ thẻ công khai.
- Tổng số flashcard trong các bộ của user.
- Số flashcard đã học từ `UserProgresses`.
- Tổng số phiên học hoàn thành.
- Số huy hiệu đã mở khóa.
- Streak hiện tại.

### Streak

Một ngày được tính active nếu user có ít nhất một trong các sự kiện:

- Hoàn thành phiên học.
- Tạo bộ thẻ công khai.
- Mở khóa huy hiệu.

Streak là số ngày active liên tiếp tính ngược từ hôm nay. Nếu hôm nay chưa active nhưng hôm qua có, streak vẫn được giữ đến hết hôm nay.

### Timeline

Trả tối đa 20 mục mới nhất, merge theo timestamp từ:

- `StudySessions`: mode, điểm và thời gian.
- `UserAchievements`: tên huy hiệu và thời gian mở khóa.
- `FlashcardSets`: bộ thẻ công khai và thời gian tạo.

Phiên bản đầu không tạo bảng activity riêng.

## Profile Privacy

- `IsPublic` kiểm soát public profile tổng thể.
- `ShowStats`, `ShowBadges`, `ShowActivity`, `ShowPublicSets` kiểm soát từng section độc lập.
- Profile cơ bản gồm username, avatar và bio khi `IsPublic = true`.
- Owner luôn xem được mọi section.
- Email luôn ẩn.
- Service không query section bị ẩn khi người xem không phải owner.

Default cho tài khoản mới:

- `IsPublic = true`.
- Các section học tập đều mặc định ẩn.

## Profile Editing

### Username

- Trim và normalize qua Identity.
- Kiểm tra uniqueness.
- Chỉ đổi khi `LastUsernameChangedAt` null hoặc đã qua 30 ngày.
- URL profile thay đổi ngay theo username mới.
- Username cũ không redirect và trả 404.

### Email

- Đổi qua `UserManager`.
- Validate format và uniqueness.
- Phiên bản đầu chưa gửi email verification.

### Password

- Yêu cầu mật khẩu hiện tại.
- Mật khẩu mới tuân theo Identity policy đang cấu hình.
- Đổi qua `ChangePasswordAsync`.
- Refresh sign-in sau khi đổi thành công.

### Avatar crop

- Chấp nhận JPG, PNG, WebP tối đa 5 MB.
- Browser hiển thị cropper với khung tròn cố định.
- User kéo ảnh và zoom để chọn vùng.
- Client xuất ảnh crop vuông; CSS hiển thị hình tròn.
- Server decode lại file, kiểm tra MIME thực và kích thước trước khi lưu.
- File lưu với tên ngẫu nhiên trong `wwwroot/uploads/avatars`.
- Avatar cũ chỉ bị xóa sau khi avatar mới đã lưu thành công.

### Profile error handling

- Tất cả lỗi user-facing bằng tiếng Việt.
- Validation error giữ lại dữ liệu form hợp lệ đã nhập.
- Upload lỗi không thay đổi avatar hiện tại.
- Lỗi lưu file không cập nhật database.
- Lỗi database sau khi lưu file phải cleanup file mới.

## Visual Direction

Concept được chọn là **Wrong Turn**.

Trang dùng một cảnh minh họa nhẹ gồm bầu trời, các lớp đồi, biển chỉ đường và vocabulary card ở trung tâm. Card giới thiệu cụm từ `wrong turn` để biến tình huống 404 thành một mini vocabulary lesson.

Visual language:

- Màu xanh lá trầm, kem và than đậm.
- Typography serif cho từ vựng và headline; sans-serif cho mô tả và control.
- Bề mặt phẳng, bóng đổ có chủ đích, không gradient trang trí quá mức.
- Chuyển động nền rất nhẹ để không cạnh tranh với nội dung chính.
- Không dùng chuỗi ký hiệu `-->`, `<--` hoặc lạm dụng ký hiệu mũi tên trong nội dung và CTA.
- CTA dùng nhãn rõ nghĩa như `Về trang chủ` và `Lật thẻ`.

## Content

Nội dung chính:

- Status label: `404 / wrong turn`.
- Headline: `Bạn vừa rẽ nhầm một hướng.`
- Mô tả ngắn giải thích trang không tồn tại.
- Vocabulary card mặt trước:
  - Term: `wrong turn`.
  - Part of speech: noun phrase.
  - Definition: a direction that takes you somewhere unexpected.
- Vocabulary card mặt sau:
  - Example: `I took a wrong turn, but found a new route.`
  - IPA: `/rɔːŋ tɜːrn/`.
- Primary CTA: `Về trang chủ`.
- Secondary control: `Lật thẻ`.

## Interaction Design

### Card flip

- Nút `Lật thẻ` chuyển giữa mặt trước và mặt sau.
- Khi vocabulary card có focus, phím `Space` thực hiện cùng hành vi.
- Control cập nhật `aria-pressed` hoặc trạng thái tương đương để screen reader biết mặt card hiện tại.
- Focus ring luôn nhìn thấy khi điều hướng bằng bàn phím.

### Motion

- Card flip dùng animation ngắn, không vượt quá khoảng 600 ms.
- Cảnh nền có thể dùng parallax hoặc floating nhẹ.
- Khi `prefers-reduced-motion: reduce`, tắt parallax và thay card flip bằng chuyển trạng thái không animation.
- Không tự động chuyển trang và không tự động lật card.

### No-JavaScript fallback

- Heading, mô tả, mặt trước vocabulary card và link `Về trang chủ` vẫn hiển thị.
- Việc không có JavaScript không được ngăn user rời trang 404.

## Standalone Prototype

```text
prototype/404/
  index.html
  404.css
  404.js
```

Prototype không phụ thuộc Razor, Bootstrap hoặc backend. Nó là nguồn tham chiếu trực quan và behavior trước khi tích hợp MVC.

## MVC Integration

```text
Controllers/HomeController.cs
Views/Shared/NotFound.cshtml
wwwroot/css/not-found.css
wwwroot/js/not-found.js
Program.cs
```

### Request flow

1. Một route không khớp endpoint tạo response status `404`.
2. `UseStatusCodePagesWithReExecute` re-execute request đến action 404 trong `HomeController`.
3. Action trả `Views/Shared/NotFound.cshtml` và giữ `Response.StatusCode = 404`.
4. View đặt `ViewData["HideLayoutChrome"] = true` để có composition toàn màn hình.
5. View chỉ tải `not-found.css` và `not-found.js` cho trang này.

Exception `500` tiếp tục dùng `/Home/Error`; không chia sẻ action hoặc view với 404.

## Components and Responsibilities

### `HomeController.NotFound`

- Trả view 404.
- Đảm bảo status code là `404`.
- Không đọc hoặc hiển thị exception, request body hay thông tin nội bộ.

### `NotFound.cshtml`

- Chứa semantic HTML của scene, heading, card và CTA.
- Dùng link thật đến `/` cho fallback không JavaScript.
- Cung cấp ARIA labels và keyboard-focus targets.

### `not-found.css`

- Quản lý toàn bộ visual scene và responsive layout.
- Có media query cho mobile.
- Có `prefers-reduced-motion`.
- Không thêm style 404 vào `site.css`.

### `not-found.js`

- Chỉ quản lý trạng thái lật card và keyboard interaction.
- Không thực hiện navigation thay cho link HTML.
- Không thêm dependency.

## Error Handling

- Route không tồn tại trả đúng `404`, không redirect thành response `200`.
- Asset hoặc JavaScript lỗi không làm mất CTA về trang chủ.
- Không hiển thị URL nội bộ, stack trace, request ID hoặc thông tin nhạy cảm.
- Nội dung user-facing bằng tiếng Việt, ngoại trừ từ vựng tiếng Anh minh họa.
- Route hợp lệ và exception `500` không bị status-code middleware thay đổi hành vi.

## Testing

### Automated

- Route không tồn tại trả HTTP `404`.
- Response chứa heading 404 mới và CTA `Về trang chủ` trỏ đến `/`.
- Trang 404 không dùng template exception mặc định.
- Markup hiển thị không chứa `-->` hoặc `<--`.
- JavaScript hỗ trợ click và phím `Space` để lật card.
- JavaScript cập nhật accessibility state của card.
- CSS chứa rule `prefers-reduced-motion`.
- Prototype độc lập tham chiếu đúng `404.css` và `404.js`.

### Manual

- Desktop và mobile không tràn nội dung.
- Card flip hoạt động bằng chuột và bàn phím.
- Focus state rõ ràng.
- Tắt JavaScript vẫn thấy nội dung chính và link về home.
- `/Account/Login` và các route hợp lệ hoạt động bình thường.
- `/khong-ton-tai` hiển thị trang mới và trả `404`.
- Exception `500` tiếp tục dùng trang lỗi riêng.

## Acceptance Criteria

- Profile public/private đúng theo `IsPublic` và owner access.
- Privacy toggles không làm lộ section bị ẩn.
- Statistics, streak và timeline trả đúng dữ liệu và thứ tự.
- Username cooldown, uniqueness, email và password dùng đúng Identity flow.
- Avatar crop, validation và cleanup không để lại file rác.
- Prototype độc lập và bản MVC có cùng visual direction, nội dung và interaction.
- Trang 404 có concept Wrong Turn, vocabulary card và CTA về trang chủ.
- Không lạm dụng mũi tên hoặc dùng chuỗi `-->`, `<--` trong giao diện.
- Trang dùng được trên mobile, keyboard và reduced-motion mode.
- Route không tồn tại trả đúng HTTP `404`.
- Không thay đổi hành vi của route hợp lệ hoặc trang lỗi `500`.

## Delivery Order

Một implementation plan duy nhất sẽ chia thành các workstream theo thứ tự:

1. `UserProfile` entity, DbContext và migration.
2. `ProfileService` và tests cho privacy, statistics, streak, timeline và cooldown.
3. Public profile `/u/{username}` theo layout Timeline cộng đồng.
4. Edit profile, email, password và privacy controls.
5. Avatar upload/crop và `AvatarService`.
6. Prototype 404 HTML/CSS/JS.
7. MVC 404 và status-code middleware integration.
8. Full regression tests và browser smoke tests.

Profile và 404 có thể được phát triển độc lập sau bước setup; không tạo dependency kỹ thuật giữa hai module.
