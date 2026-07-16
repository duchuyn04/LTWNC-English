# Thiết kế: nhập thẻ từ tệp

## Mục tiêu

Cho phép chủ sở hữu của một bộ thẻ đã tồn tại nhập nhanh nhiều thẻ từ một tệp `.csv` hoặc `.xlsx`, thay cho việc tạo từng thẻ bằng biểu mẫu thủ công.

## Phạm vi

- Điểm vào là trang quản lý bộ thẻ `/Set/{id}/Edit`.
- Chỉ chủ sở hữu bộ thẻ được nhập dữ liệu.
- Hỗ trợ tệp CSV UTF-8 và Excel XLSX; với XLSX, đọc worksheet đầu tiên.
- Chỉ thêm thẻ mới vào bộ thẻ hiện tại. Không cập nhật, xóa, hay ghi đè thẻ đang có.
- Cả hai định dạng dùng hàng đầu tiên làm header và tên cột chuẩn, không phân biệt hoa/thường và bỏ khoảng trắng đầu/cuối.

## Định dạng tệp

Sáu cột bắt buộc, theo thứ tự bất kỳ:

| Header | Trường thẻ |
| --- | --- |
| `Thuật ngữ` | `FrontText` |
| `Định nghĩa` | `BackText` |
| `IPA` | `Pronunciation` |
| `Loại từ` | `PartOfSpeech` |
| `Ví dụ tiếng Anh` | `ExampleSentence` |
| `Nghĩa ví dụ tiếng Việt` | `ExampleMeaning` |

Hai cột tùy chọn:

| Header | Trường thẻ |
| --- | --- |
| `Từ đồng nghĩa` | `Synonyms` |
| `URL ảnh` | `ImageUrl` |

Tệp thiếu bất kỳ header bắt buộc nào bị từ chối trước khi nhập. Các hàng trống hoàn toàn bị bỏ qua và không tính là lỗi. File mẫu được cung cấp trên giao diện với các header trên và một hàng dữ liệu minh họa.

## Trải nghiệm người dùng

Trang Edit có một khối “Nhập thẻ từ file”, gồm:

- Trường chọn tệp, chỉ nhận `.csv` và `.xlsx`.
- Ghi chú về sáu cột bắt buộc, hai cột tùy chọn và liên kết tải file mẫu.
- Nút nhập.

Sau khi gửi, người dùng quay lại trang Edit của cùng bộ thẻ. Thông báo thành công nêu số thẻ đã nhập. Nếu có hàng bị bỏ qua, một báo cáo lỗi nêu số dòng tệp và lý do từng lỗi được hiển thị cùng thông báo; các hàng hợp lệ vẫn được lưu.

## Luồng xử lý

1. Controller nhận tệp và user hiện tại, sau đó yêu cầu import service xử lý theo `setId`.
2. Service xác nhận bộ thẻ tồn tại và thuộc về user; nếu không, từ chối thao tác.
3. Service kiểm tra tệp có nội dung, phần mở rộng hợp lệ, kích thước giới hạn an toàn và cấu trúc đọc được.
4. Parser tương ứng đọc bảng thành các hàng, xác minh header, rồi ánh xạ mỗi hàng thành dữ liệu thẻ.
5. Mỗi hàng được xác thực theo đúng quy tắc tạo thẻ hiện có: sáu giá trị bắt buộc không rỗng, `PartOfSpeech` tối đa 80 ký tự; trường tùy chọn được trim và đổi thành `null` khi rỗng.
6. Một hàng sai được ghi vào danh sách lỗi với số dòng gốc và không chặn các hàng khác.
7. Tất cả hàng hợp lệ được thêm với `OrderIndex` tiếp nối thẻ cuối hiện có, đúng thứ tự trong file. `UpdatedAt` của set được cập nhật khi có ít nhất một thẻ được thêm.
8. Service trả về kết quả gồm số hàng đã thêm, số hàng bị bỏ qua và các lỗi chi tiết.

## Kiến trúc

Tạo domain service import riêng trong `Services/FlashcardSets` với contract rõ ràng, nhận stream/tệp, `setId` và `userId`, trả về result model. Service này điều phối xác thực quyền, parse và lưu dữ liệu. Các parser CSV/XLSX nằm sau cùng một abstraction để controller không phụ thuộc vào định dạng tệp. `FlashcardSetController` chỉ xử lý HTTP, antiforgery, TempData và redirect.

Việc tạo `Flashcard` dùng chung quy tắc chuẩn hóa với luồng thêm thủ công để tránh hai cách xử lý dữ liệu khác nhau. Import không hỗ trợ upload ảnh cục bộ; chỉ URL ảnh từ cột tùy chọn.

## Xử lý lỗi và an toàn

- File không được chọn, đuôi không hỗ trợ, quá dung lượng, không đọc được hoặc thiếu header bắt buộc: không thêm thẻ và báo lỗi tổng quát.
- Hàng dữ liệu không đủ trường bắt buộc hoặc có `PartOfSpeech` quá dài: bỏ qua hàng và báo số dòng/lý do.
- CSV phải đọc đúng trường có dấu ngoặc kép, dấu phẩy và xuống dòng theo chuẩn CSV.
- Không suy đoán header hoặc tự ánh xạ cột tùy ý trong phiên bản này.
- Chỉ chấp nhận XLSX; không hỗ trợ XLS cũ hay macro-enabled XLSM.
- Không lưu tệp upload sau khi xử lý.

## Kiểm thử

- CSV hợp lệ: thêm đúng dữ liệu, trường tùy chọn, thứ tự và cập nhật thời gian bộ thẻ.
- XLSX hợp lệ: cùng kỳ vọng như CSV.
- Hỗn hợp hợp lệ/lỗi: chỉ lưu hàng hợp lệ và result có số dòng, lý do chính xác.
- Thiếu header, tệp rỗng, phần mở rộng không hỗ trợ và tệp hỏng: không lưu bất kỳ thẻ nào.
- User không sở hữu bộ thẻ: từ chối và không thay đổi dữ liệu.
- Xác nhận thứ tự import bắt đầu sau `OrderIndex` lớn nhất của thẻ hiện có.

## Ngoài phạm vi

- Tạo bộ thẻ trực tiếp từ file.
- Ánh xạ header tùy ý hoặc bước xem trước/chỉnh sửa dữ liệu trước khi lưu.
- Import hình ảnh nhị phân.
- Cập nhật thẻ đã có hoặc phát hiện thẻ trùng lặp tự động.
