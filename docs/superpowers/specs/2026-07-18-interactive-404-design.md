# Interactive 404 Page Design

**Date:** 2026-07-18

## Goal

Xây dựng một trang 404 sáng tạo bằng HTML, CSS và JavaScript, sau đó tích hợp cùng thiết kế vào ứng dụng ASP.NET Core MVC LTWNC. Trang biến lỗi điều hướng thành một khoảnh khắc học từ vựng ngắn, nhưng vẫn ưu tiên khả năng quay lại ứng dụng nhanh chóng.

## Scope

### In scope

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

- Mini-game hoặc hệ thống điểm.
- Âm thanh hoặc phát âm tự động.
- Tracking và analytics cho lỗi 404.
- Thay đổi trang lỗi 500.
- Thư viện animation hoặc UI mới.
- Tìm kiếm route hoặc tự động đoán URL user muốn truy cập.

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

- Prototype độc lập và bản MVC có cùng visual direction, nội dung và interaction.
- Trang 404 có concept Wrong Turn, vocabulary card và CTA về trang chủ.
- Không lạm dụng mũi tên hoặc dùng chuỗi `-->`, `<--` trong giao diện.
- Trang dùng được trên mobile, keyboard và reduced-motion mode.
- Route không tồn tại trả đúng HTTP `404`.
- Không thay đổi hành vi của route hợp lệ hoặc trang lỗi `500`.
