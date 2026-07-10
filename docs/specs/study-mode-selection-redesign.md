# Redesign trang chọn chế độ học (`/Study/{setId}`)

## Tóm tắt

Tái thiết kết flow từ khi user nhấn **Học** trên bộ thẻ đến khi vào chế độ học cụ thể. Hiện tại trang `/Study/{setId}` hiển thị 5 ô lớn, trong đó 2 ô bị disabled, không có context tiến độ, và để user bị redirect về lại trang này khi filter không khả dụng.

## Mục tiêu

- Giảm cognitive load: một CTA chính rõ ràng.
- Cung cấp context bộ thẻ trước khi học: số từ, tiến độ, số lần học gần đây, thờ gian dự kiến.
- Phân biệt mode khả dụng vs mode sắp ra mắt.
- Xử lý empty-state/filter conflict inline, không redirect mù.
- Áp dụng phong cách **Editorial Luxury** theo taste skills: warm cream, serif heading, không gian thoáng.

## Quyết định thiết kế

| # | Quyết định | Lý do |
|---|-----------|-------|
| 1 | Nút **Học** trên set card là entry duy nhất, vào `/Study/{setId}` | Giảm bước, user biết rõ đang chọn bộ thẻ nào. |
| 2 | Một CTA chính = mode được gợi ý; các mode khác là chip/tab phụ | Giảm lựa chọn, tăng tỷ lệ bắt đầu. |
| 3 | "Chỉ đã sao" / "Chỉ chưa thuộc" là filter, không phải mode | Đúng ngữ nghĩa domain; áp dụng cho mọi mode. |
| 4 | Preview trên trang chọn chế độ | User biết mình sắp học gì, tránh click mù. |
| 5 | Empty-state/filter conflict xử lý inline | Trải nghiệm liền mạch, không bị đẩy qua lại giữa các trang. |
| 6 | Roadmap modes chưa ra mắt ở footer | Không chiếm vị trí chính, vẫn gợi ý tính năng tương lai. |
| 7 | Vibe Editorial Luxury + Layout Editorial Split | Phù hợp app học tiếng Anh, tập trung, đọc sách, ít distraction. |

## Logic gợi ý mode mặc định

```
if (bộ thẻ chưa có thẻ nào đã thuộc)
    gợi ý Flashcard
else if (đã thuộc >= 50% && có ít nhất 1 thẻ có ExampleSentence)
    gợi ý Nghe chép
else
    gợi ý Flashcard

Nếu filter đang bật làm mode gợi ý không khả dụng
→ fallback sang mode khả dụng + hiện banner lý do.
```

## Dữ liệu hiển thị

- Tên bộ thẻ + mô tả (nếu có).
- Tổng số từ.
- Số từ đã thuộc / % tiến độ.
- Số từ đã sao.
- Số lần học bộ thẻ trong 7 ngày qua.
- Thờigian dự kiến: `Flashcard ~15s/thẻ`, `Nghe chép ~25s/thẻ` × số thẻ sau filter.
- Filter nhanh: "Chỉ đã sao", "Chỉ chưa thuộc".

## Layout chi tiết

### Desktop (≥992px): Editorial Split

- **Cột trái (5/12):**
  - Eyebrow tag: "Bộ thẻ".
  - Tên bộ thẻ: serif, lớn, tight tracking.
  - Mô tả (nếu có): muted sans-serif.
  - Progress bar + stats grid (tổng từ, đã thuộc, đã sao, lần học tuần này).
  - Thờigian dự kiến.
  - Quick filters: 2 toggle pills.
  - Primary CTA: "Bắt đầu [Tên mode]".
- **Cột phải (7/12):**
  - Card mode chính lớn (CTA dự phòng).
  - 1–2 card mode phụ dạng chip/thumbnail.
  - Banner inline nếu mode nào đó không khả dụng do filter.
- **Footer full-width:**
  - Roadmap strip: "Sắp ra mắt: Trắc nghiệm · Ghép đôi".

### Mobile (<768px)

Stack dọc:
1. Header + progress + stats.
2. Primary CTA.
3. Mode chips.
4. Inline empty-state (nếu có).
5. Roadmap strip.

## Visual direction

- **Background:** `#FDFBF7` (warm cream) hoặc `#FBFBFA`.
- **Text:** `#111111` primary, `#787774` secondary.
- **Borders:** `1px solid #EAEAEA`.
- **Cards:** `#FFFFFF`, radius `12px`, padding `24px–32px`.
- **CTA:** solid `#111111`, text `#FFFFFF`, radius `6px`, hover `#333333`, active `scale(0.98)`.
- **Serif heading:** `Newsreader` / `Playfair Display` / `Instrument Serif` fallback `Georgia, serif`.
- **Body/UI:** `Geist Sans`, `SF Pro Display`, `Helvetica Neue`, `system-ui`.
- **Icon:** Phosphor Icons (Bold), stroke weight thống nhất.
- **Motion:**
  - Scroll entry: `translateY(12px)` + opacity, `600ms`, `cubic-bezier(0.16, 1, 0.3, 1)`.
  - Hover card: `box-shadow 0 2px 8px rgba(0,0,0,0.04)`, transition `200ms`.
  - Button active: `scale(0.98)`.

## Thay đổi code

### Backend

- `Controllers/StudyController.cs`:
  - Action `Index(int setId)` trả về `StudyModeSelectorViewModel` thay vì ViewBag.
  - Tính toán stats, recommended mode, availability từng mode, estimated time.
- `Services/StudyService.cs`:
  - Thêm `GetStudyModeSelectorDataAsync(int setId, string? userId)`.
  - Đếm session 7 ngày qua.
- `Models/ViewModels/Study/StudyModeSelectorViewModel.cs` (mới).

### Frontend

- `Views/Study/Index.cshtml`: rewrite theo layout mới.
- `wwwroot/css/study-mode-selector.css` (mới): style chuyên biệt cho trang.
- `Views/FlashcardSet/Index.cshtml`: nâng cấp nút **Học** thành primary button rõ ràng.

### Không thay đổi

- Các action mode (`Flashcard`, `Dictation`, ...) giữ nguyên route và behavior.
- Settings chi tiết (answer mode, shuffle, dictation content mode) vẫn ở trong màn hình học.

## Tiêu chí chấp nhận

- [ ] Trang `/Study/{setId}` hiển thị đúng layout Editorial Split trên desktop.
- [ ] CTA chính luôn là mode khả dụng và phù hợp với tiến độ bộ thẻ.
- [ ] Filter nhanh cập nhật real-time (hoặc AJAX reload) số từ khả dụng và CTA.
- [ ] Khi filter loại hết thẻ, hiện inline banner + hướng dẫn, không redirect mù.
- [ ] Mode chưa ra mắt không hiển thị dạng card clickable.
- [ ] Nút **Học** trên `/FlashcardSet/Index` có style primary button rõ ràng.
- [ ] Responsive trên mobile stack dọc, không bị tràn layout.

## Notes

- Không thêm dependency font/icon mới nếu không cần thiết. Dùng Google Fonts hoặc system stack tùy môi trường.
- Thờigian dự kiến là ước tính đơn giản, không cần thêm bảng đo thực tế lúc này.
