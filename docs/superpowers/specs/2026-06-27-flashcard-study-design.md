# Đặc tả thiết kế: Module học Flashcard Premium với Âm thanh (TTS) và Hiệu ứng Pháo hoa

Tài liệu này mô tả chi tiết thiết kế giao diện (UI/UX) và kiến trúc kỹ thuật cho việc nâng cấp chức năng học Flashcard của ứng dụng LTWNC English.

## 1. Mục tiêu (Goals)
* Nâng cấp giao diện học Flashcard hiện tại thành giao diện sang trọng, cao cấp (Apple/Notion-like) ở chế độ **Light Mode** đồng bộ với hệ thống.
* Trải nghiệm chuyển thẻ mượt mà không tải lại trang (Single Page / AJAX transition).
* Tích hợp phát âm thanh tự động (Text-to-Speech) khi lật mặt sau, hỗ trợ tiếng Anh (Nam/Nữ) và tiếng Việt.
* Thêm hiệu ứng pháo hoa chúc mừng bằng Canvas khi người dùng hoàn thành toàn bộ bộ thẻ.
* Hỗ trợ phím tắt bàn phím (Keyboard Shortcuts) giúp người học thao tác nhanh.

## 2. Thiết kế Giao diện (UI/UX Design)
* **Chủ đề màu sắc:** Light Mode dựa trên màu nền kem nhạt `#F7F6F3` đặc trưng của Notion, kết hợp với các thẻ trắng (`#ffffff`) viền kép tinh tế (Double Bezel).
* **Trạng thái học tập:**
  * **Trang học thẻ:** Hiển thị tiến trình dạng phần trăm hiện đại, Flashcard lật 3D ở trung tâm, và bảng điều khiển âm thanh + điều hướng dưới chân thẻ.
  * **Trang hoàn thành:** Chuyển sang bố cục dạng Bento Box thống kê số thẻ "Đã thuộc" vs "Cần học lại" kèm hiệu ứng pháo hoa Canvas rực rỡ và các nút hành động tiếp theo.

## 3. Giải pháp Kỹ thuật (Technical Implementation)

### A. Client-Side JavaScript
* **Đồng bộ hóa danh sách thẻ:** Thay vì tải từng thẻ một từ server, toàn bộ danh sách thẻ của bộ học phần sẽ được chuyển sang định dạng JSON và nhúng trực tiếp vào Razor View:
  ```html
  <script>
    const flashcards = @Html.Raw(Json.Serialize(Model.Flashcards.Select(c => new { c.Id, c.FrontText, c.BackText })));
  </script>
  ```
* **Quản lý trạng thái:** JavaScript sẽ quản lý chỉ mục thẻ hiện tại (`currentIndex`). Khi người dùng nhấn nút "Trước"/"Sau", JavaScript cập nhật nội dung thẻ, phát âm thanh và chuyển đổi hiệu ứng 3D mà không tải lại trang.
* **Tích hợp Web Speech API (Phát âm):**
  * Sử dụng API `window.speechSynthesis` có sẵn trong trình duyệt (không phát sinh chi phí API ngoài).
  * Lọc danh sách giọng đọc của hệ thống để hiển thị tùy chọn giọng tiếng Anh (US/UK, Nam/Nữ) và giọng tiếng Việt.
  * Lưu các tùy chọn cấu hình của người dùng (Bật/tắt tự phát âm, giọng phát âm đã chọn, tốc độ đọc) vào `window.localStorage` để giữ nguyên tùy chọn ở những buổi học sau.
* **Đồng bộ tiến trình (AJAX):**
  * Khi nhấn "Đã biết" hoặc "Chưa biết", client gửi yêu cầu AJAX POST ngầm lên endpoint `/Study/{setId}/Flashcard/Mark` để cập nhật trạng thái trong database mà không làm gián đoạn trải nghiệm học.
* **Hiệu ứng pháo hoa (Canvas):**
  * Tích hợp một script vẽ hạt pháo hoa bằng Canvas cực nhẹ, tự động kích hoạt khi người dùng hoàn thành thẻ cuối cùng và chuyển sang màn hình kết quả.

### B. Thay đổi Codebase (Proposed Changes)

#### [MODIFY] [StudyController.cs](file:///c:/it/ltwnc/Controllers/StudyController.cs)
* Cập nhật các action `MarkLearned` và `Complete` để kiểm tra xem request có phải là AJAX (`Request.Headers["X-Requested-With"] == "XMLHttpRequest"`) hay không.
* Nếu là AJAX request, trả về phản hồi JSON `{ success = true }` thay vì thực hiện `RedirectToAction` tải lại toàn bộ trang.

#### [MODIFY] [Flashcard.cshtml](file:///c:/it/ltwnc/Views/Study/Flashcard.cshtml)
* Viết lại toàn bộ mã HTML/CSS/JS của trang học flashcard theo thiết kế Light Mode đã được phê duyệt.
* Tích hợp logic xử lý JavaScript (chuyển thẻ, lật thẻ, phím tắt, TTS, Canvas pháo hoa và AJAX).

## 4. Kế hoạch Kiểm thử & Xác minh (Verification Plan)

### Kiểm thử Thủ công (Manual Verification)
1. Truy cập vào một bộ thẻ có từ vựng và chọn chế độ học **Flashcard**.
2. Kiểm tra hiệu ứng lật thẻ khi click chuột hoặc nhấn phím `Space`.
3. Kiểm tra tính năng phát âm:
   * Khi lật sang mặt sau, âm thanh có phát tự động không.
   * Đổi giọng đọc sang giọng Nam/Nữ và kiểm tra giọng đọc có thay đổi tương ứng.
   * Tắt tùy chọn "Tự phát âm", lật thẻ và xác minh không có âm thanh phát ra.
4. Chuyển đổi giữa các thẻ bằng phím mũi tên `←` / `→` và nút bấm trên màn hình.
5. Đánh dấu thẻ "Đã biết" / "Chưa biết" và kiểm tra xem database có ghi nhận tiến trình học (thông qua xem bảng `UserProgresses` hoặc log của ứng dụng).
6. Đi đến thẻ cuối cùng, nhấn hoàn thành và kiểm tra xem màn hình ăn mừng có hiển thị đúng thống kê và pháo hoa hoạt họa Canvas có hoạt động mượt mà không.
