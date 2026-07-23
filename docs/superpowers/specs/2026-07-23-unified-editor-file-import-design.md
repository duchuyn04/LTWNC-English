# Thiết kế: khôi phục import file trong Unified Editor

## Mục tiêu

Khôi phục khả năng nhập flashcard từ CSV/XLSX trong giao diện Unified Editor hiện tại mà không loại bỏ chức năng dán nhanh. Người dùng được xem trước dữ liệu, chọn thêm mới hoặc ghi đè toàn bộ, và nhận báo cáo lỗi rõ ràng trước lẫn sau khi nhập.

## Hiện trạng và nguyên nhân

Backend import file vẫn hỗ trợ CSV UTF-8 và XLSX qua `IFlashcardImportService`, nhưng giao diện chỉnh sửa hiện đã chuyển từ `Edit.cshtml` sang `Editor.cshtml`. Form upload file cũ vì vậy không còn xuất hiện. Modal Import mới trong Unified Editor chỉ phân tích nội dung dán và gửi `FrontText`/`BackText` vào batch API. File CSV mẫu cũng không còn trong cây source hiện tại.

## Phạm vi

- Nâng cấp modal Import hiện có trong `Views/FlashcardSet/Editor.cshtml`.
- Giữ hai cách nhập trong cùng modal:
  - `Tải file` là tab mặc định, nhận `.csv` và `.xlsx`.
  - `Dán nhanh` giữ hành vi nhập thuật ngữ/định nghĩa hiện có.
- Hỗ trợ hai chế độ cho cả hai tab:
  - `Thêm vào bộ thẻ`.
  - `Ghi đè toàn bộ`.
- Khi editor đang tạo bộ thẻ mới chưa lưu, hỏi người dùng có muốn lưu bộ thẻ để tiếp tục import hay không.
- Xem trước file trên backend trước khi cho phép xác nhận import.
- Khôi phục CSV mẫu.
- Không thay đổi định dạng dữ liệu flashcard hoặc bổ sung định dạng file khác.

## Trải nghiệm người dùng

### Mở modal

Nút `Import` hiện tại trên thanh công cụ tiếp tục mở modal theo ngôn ngữ thiết kế của Unified Editor. Modal dùng hai tab có trạng thái chọn rõ ràng và hỗ trợ bàn phím:

1. `Tải file`.
2. `Dán nhanh`.

Nếu bộ thẻ chưa có `setId`, ứng dụng hiển thị xác nhận: người dùng có muốn lưu bộ thẻ chưa lưu để tiếp tục hay không. Khi đồng ý, editor kiểm tra tên bộ thẻ, lưu tên/mô tả/trạng thái công khai bằng luồng tạo set hiện có, cập nhật URL/state của editor rồi mở modal. Khi từ chối hoặc tên không hợp lệ, import dừng và không tạo set.

### Tab tải file

Tab mặc định gồm:

- Vùng chọn hoặc kéo thả một file `.csv`/`.xlsx`.
- Tên file, dung lượng và trạng thái file đã chọn.
- Liên kết tải CSV mẫu.
- Ghi chú ngắn về sáu cột bắt buộc và hai cột tùy chọn.
- Nhóm lựa chọn `Thêm vào bộ thẻ` hoặc `Ghi đè toàn bộ`; mặc định là thêm vào.
- Nút `Xem trước`.

Sau khi xem trước thành công, modal hiển thị:

- Tổng số dòng hợp lệ.
- Tổng số dòng lỗi/bị bỏ qua.
- Bảng tối đa năm dòng hợp lệ đầu tiên với đầy đủ trường.
- Danh sách lỗi gồm số dòng và lý do; UI có thể giới hạn số lỗi hiển thị nhưng phải nêu số lỗi còn lại.
- Nút `Nhập thẻ`, chỉ bật khi còn ít nhất một dòng hợp lệ.

Thay file hoặc đổi chế độ nhập làm mất hiệu lực preview cũ và yêu cầu xem trước lại.

### Tab dán nhanh

Giữ cách chọn dấu phân cách, textarea, preview thuật ngữ/định nghĩa và batch API hiện có. Chuyển lựa chọn thêm/ghi đè thành nhóm lựa chọn dùng chung về mặt trình bày với tab tải file. Nội dung người dùng dán tiếp tục được dựng bằng DOM và `textContent`, không đưa trực tiếp vào `innerHTML`.

### Xác nhận và kết quả

Với chế độ ghi đè, trước lần ghi cuối cùng phải có xác nhận rõ rằng toàn bộ thẻ và tiến độ học liên quan của bộ hiện tại sẽ bị xóa. Nếu người dùng hủy, không có thay đổi database.

Sau khi nhập file:

- Editor tải lại dữ liệu để danh sách thẻ phản ánh chính xác database.
- Thông báo nêu số thẻ đã nhập và số dòng bị bỏ qua.
- Nếu có lỗi từng dòng, báo cáo vẫn có thể xem được sau khi tải lại.
- File-level error và preview error hiển thị ngay trong modal, modal không đóng.

## Định dạng file

Sáu header bắt buộc, không phân biệt hoa/thường và được trim:

| Header | Trường |
| --- | --- |
| `Thuật ngữ` | `FrontText` |
| `Định nghĩa` | `BackText` |
| `IPA` | `Pronunciation` |
| `Loại từ` | `PartOfSpeech` |
| `Ví dụ tiếng Anh` | `ExampleSentence` |
| `Nghĩa ví dụ tiếng Việt` | `ExampleMeaning` |

Hai header tùy chọn:

| Header | Trường |
| --- | --- |
| `Từ đồng nghĩa` | `Synonyms` |
| `URL ảnh` | `ImageUrl` |

CSV phải là UTF-8 và tuân thủ quy tắc CSV chuẩn cho dấu phẩy, dấu ngoặc kép và xuống dòng trong trường. XLSX chỉ đọc worksheet đầu tiên. File tối đa 10 MB và tối đa 5.000 dòng dữ liệu.

## Kiến trúc và luồng dữ liệu

### Preview

Thêm endpoint POST preview nhận `setId` và multipart file. Endpoint:

1. Xác thực đăng nhập, quyền sở hữu set, antiforgery, rate limit và giới hạn file.
2. Dùng parser/resolver hiện có để đọc file.
3. Không gọi `SaveChangesAsync` và không thay đổi database.
4. Trả JSON gồm số dòng hợp lệ, số dòng lỗi, tối đa năm dòng hợp lệ đầu tiên, danh sách lỗi giới hạn và tổng số lỗi bị ẩn.

Preview không tạo token tin cậy và không thay thế kiểm tra lúc import. Trình duyệt giữ đối tượng `File` đã chọn; khi xác nhận, file được gửi lại cho endpoint import và backend parse/validate lại.

### Commit import

Mở rộng import service để nhận `replaceAll`. Sau khi parse và xác thực lại:

- `replaceAll = false`: thêm các dòng hợp lệ sau `OrderIndex` lớn nhất hiện tại.
- `replaceAll = true`: chỉ khi có ít nhất một dòng hợp lệ, dùng logic batch hiện có để xóa thẻ và tiến độ liên quan rồi thêm thẻ mới trong cùng một lần lưu/transaction.
- Nếu parse lỗi toàn file, không xóa hay thêm dữ liệu.
- Các dòng lỗi bị bỏ qua; các dòng hợp lệ vẫn được nhập.
- Kiểm tra quyền sở hữu được thực hiện lại khi commit.

Endpoint import hiện có tiếp tục hỗ trợ form POST và redirect để không phá vỡ contract cũ. Unified Editor có thể submit form multipart sau preview, rồi nhận kết quả qua TempData khi trang tải lại.

### Tách trách nhiệm

- `FlashcardImportService`: xác thực file, parse, tạo preview và điều phối commit.
- `FlashcardSetService.BatchImportCardsAsync`: giữ trách nhiệm thay thế/thêm thẻ nguyên tử và dọn tiến độ khi ghi đè.
- `FlashcardSetController`: HTTP, quyền truy cập, antiforgery, response JSON cho preview và TempData/redirect cho commit.
- `Editor.cshtml`: markup modal, tabs, form multipart, thông báo kết quả.
- `unified-editor.js`: trạng thái modal, lưu set chưa lưu, upload preview, xác nhận ghi đè và submit.

## Xử lý lỗi và an toàn

- Không có file, file rỗng, sai phần mở rộng, quá 10 MB, file hỏng hoặc thiếu header: không cho commit và hiển thị lỗi cấp file.
- Hàng thiếu trường bắt buộc hoặc `Loại từ` quá 80 ký tự: đánh dấu lỗi theo số dòng, không chặn hàng hợp lệ.
- Preview và commit đều yêu cầu owner của set.
- Cả preview và commit đều có antiforgery và upload rate limit.
- Không lưu file upload trên server.
- Nội dung file luôn được encode khi render; không dùng nội dung file làm HTML.
- Ghi đè không chạy nếu kết quả parse không có dòng hợp lệ.
- Lỗi trong quá trình ghi đè phải rollback, giữ nguyên thẻ cũ.

## Khả năng truy cập và responsive

- Tab dùng semantics phù hợp (`role="tablist"`, `role="tab"`, `aria-selected`, liên kết panel).
- Modal quản lý focus khi mở/đóng, đóng bằng Escape và trả focus về nút Import.
- Vùng chọn file có input thật, label bấm được và hỗ trợ bàn phím; kéo thả chỉ là tiện ích bổ sung.
- Trạng thái preview/import dùng vùng `aria-live`.
- Trên màn hình nhỏ, bảng preview cho phép cuộn ngang; nút hành động xếp dọc nhưng vẫn giữ nút chính dễ thấy.
- Chuyển động tuân theo `prefers-reduced-motion`.

## Kiểm thử

- Markup modal có hai tab, file input `.csv,.xlsx`, template link, mode add/replace và vùng preview.
- Bộ thẻ chưa lưu: hỏi xác nhận; từ chối không tạo set; đồng ý tạo set rồi tiếp tục.
- Preview CSV/XLSX hợp lệ trả đúng counts và năm dòng đầu, không thay đổi database.
- Preview file lỗi/thiếu header trả lỗi rõ ràng.
- Import thêm tiếp nối thứ tự hiện có.
- Import ghi đè xóa thẻ và tiến độ cũ rồi tạo đầy đủ tám trường.
- Ghi đè với file không có dòng hợp lệ giữ nguyên dữ liệu cũ.
- Non-owner bị từ chối ở cả preview và commit.
- JS reset preview khi đổi file hoặc chế độ.
- Full unit/integration suite, build và browser smoke test trên desktop/mobile.

## Ngoài phạm vi

- Ánh xạ header tùy ý.
- Chỉnh sửa từng ô trực tiếp trong preview.
- Import XLS/XLSM hoặc ảnh nhị phân.
- Phát hiện và hợp nhất flashcard trùng lặp.
- Lưu file tạm để tái sử dụng giữa preview và commit.
