# Thư viện bộ thẻ công khai

## Mục tiêu

Chuyển phương án A của prototype `/prototype/library` thành trang production `/Library` dùng dữ liệu thật. Trang giúp khách và người dùng đã đăng nhập tìm, đánh giá nhanh và mở các bộ thẻ cộng đồng; `/Set` tiếp tục là thư viện cá nhân.

## Phạm vi chức năng

### Truy cập và điều hướng

- `GET /Library` cho phép truy cập ẩn danh.
- Menu chính và footer có liên kết `Thư viện` tới `/Library`.
- Liên kết `Bộ thẻ của tôi` vẫn trỏ tới `/Set` và vẫn yêu cầu đăng nhập.
- CTA khám phá bộ thẻ công khai trên trang chủ dẫn tới `/Library`.

### Dữ liệu hiển thị

Chỉ hiển thị `FlashcardSet` thỏa cả hai điều kiện:

- `IsPublic == true`.
- `ModerationStatus == Active`.

Mỗi kết quả hiển thị:

- Tiêu đề và mô tả.
- Username và chữ viết tắt đại diện của tác giả.
- Tổng số thẻ.
- Tổng số bản sao, tính từ các set có `SourceSetId` trỏ tới set nguồn.
- Thời điểm cập nhật.
- Liên kết `Xem bộ` tới `/Set/{id}`.

Màu nhấn của card được chọn ổn định theo ID bộ thẻ; không lưu thêm dữ liệu trình bày vào database.

### Thống kê tổng quan

Hero hiển thị số liệu thật trên toàn bộ thư viện công khai đang hoạt động:

- Tổng số bộ thẻ.
- Tổng số thẻ.
- Tổng số lượt sao chép.

Các số liệu không bị giới hạn bởi trang hiện tại hoặc từ khóa tìm kiếm.

### Tìm kiếm, sắp xếp và phân trang

- `q` được trim và tìm không phân biệt hoa thường trên tiêu đề, mô tả hoặc username tác giả.
- `sort` nhận một trong ba giá trị:
  - `popular`: nhiều bản sao trước; hòa thì ưu tiên cập nhật mới hơn.
  - `recent`: cập nhật mới nhất trước.
  - `cards`: nhiều thẻ trước; hòa thì ưu tiên cập nhật mới hơn.
- Giá trị `sort` không hợp lệ được chuẩn hóa về `popular`.
- `page` nhỏ hơn 1 được chuẩn hóa thành 1.
- Mỗi trang có 12 bộ thẻ.
- Nếu `page` vượt tổng số trang và có kết quả, dùng trang cuối cùng.
- Form tìm kiếm và các liên kết phân trang giữ nguyên lựa chọn sắp xếp.

### Trạng thái giao diện

- Không có dữ liệu: hiển thị CTA tạo bộ thẻ hoặc quay về trang chủ tùy trạng thái đăng nhập.
- Không có kết quả tìm kiếm: hiển thị từ khóa hiện tại và nút xóa bộ lọc.
- Mô tả rỗng dùng copy dự phòng ngắn, không để card bị trống.
- Username rỗng dùng nhãn `Thành viên` và chữ đại diện `TV`.

## Kiến trúc

### Public library service

Tạo `IPublicLibraryService` và `PublicLibraryService` riêng thay vì tiếp tục mở rộng `FlashcardSetService`, vốn đang chịu trách nhiệm CRUD, copy và editor.

Service nhận `PublicLibraryQuery` gồm `Search`, `Sort` và `Page`, sau đó trả `PublicLibraryResult` gồm:

- Query đã chuẩn hóa.
- Tổng số kết quả đã lọc và thông tin phân trang.
- Danh sách `PublicLibrarySetItem` đã projection trực tiếp từ EF.
- `PublicLibrarySummary` cho số liệu toàn thư viện.

Query dùng projection và aggregate trong database; không `Include` toàn bộ flashcards hoặc clones. Tất cả query chỉ đọc dùng `AsNoTracking`.

### MVC boundary

`LibraryController.Index` chỉ nhận query-string, gọi service và map result sang `LibraryIndexViewModel`. Controller không tự dựng truy vấn EF.

Razor view `Views/Library/Index.cshtml` là bản production rút gọn từ phương án A. View không chứa dữ liệu minh họa, switcher A/B/C, nút yêu thích giả hoặc topic giả.

### Tài sản giao diện

- Tạo `wwwroot/css/library.css` từ phần CSS thực sự được dùng bởi phương án A.
- Chỉ thêm JavaScript nếu cần shortcut `/` để focus ô tìm kiếm; tìm kiếm, sắp xếp và phân trang vẫn hoạt động hoàn toàn bằng GET khi JavaScript tắt.
- Xóa `LibraryPrototypeController`, `Views/Prototype/Library.cshtml`, `library-prototype.css` và `library-prototype.js` sau khi trang production thay thế hoàn chỉnh.
- Không xóa `Models/IPrototype.cs`; đây là abstraction clone của domain, không liên quan prototype giao diện.

## Bảo mật và tính đúng đắn

- Filter công khai và moderation phải nằm trong service query trước mọi search, sort hoặc projection.
- Không expose email tác giả.
- Không thêm endpoint thay đổi dữ liệu mới; sao chép vẫn dùng endpoint POST có authorization và antiforgery hiện có trên trang chi tiết.
- Không cần migration database.
- Query không hợp lệ không gây exception và không được đưa trực tiếp vào tên cột hoặc dynamic SQL.

## Kiểm thử

### Service tests

- Loại bộ riêng tư và bộ bị cách ly khỏi kết quả lẫn summary.
- Tìm theo tiêu đề, mô tả và username.
- Đếm thẻ và bản sao đúng.
- Ba kiểu sắp xếp cho thứ tự xác định.
- Chuẩn hóa `sort`, `page` và clamp trang vượt giới hạn.
- Phân trang 12 phần tử.

### Controller và route tests

- `/Library` truy cập ẩn danh và trả đúng view model.
- Query-string được truyền đúng sang service.
- `/Set` vẫn yêu cầu đăng nhập.
- Route prototype cũ trả `404` sau khi bị xóa.

### Markup tests

- View có form GET, các lựa chọn sort, summary và pager.
- Mỗi card liên kết `/Set/{id}`.
- Không còn nhãn `PROTOTYPE`, switcher variant, dữ liệu minh họa hoặc favorite giả.
- Layout có đồng thời `Thư viện` và `Bộ thẻ của tôi` trỏ đúng route.

### Verification

- Chạy test tập trung trong chu trình đỏ–xanh.
- Chạy toàn bộ `dotnet test`.
- Chạy `dotnet build --no-restore`.
- Khởi chạy ứng dụng và kiểm tra trực quan `/Library` ở desktop và mobile, gồm empty/search/pagination khi dữ liệu cho phép.

## Ngoài phạm vi

- Yêu thích bộ thẻ.
- Topic/category do người dùng gán.
- Xếp hạng theo xu hướng thời gian thực.
- Thay đổi schema hoặc migration.
- Thay đổi luồng copy, báo cáo nội dung hoặc trang chi tiết bộ thẻ.

## Tiêu chí hoàn thành

- `/Library` là trang production công khai dùng dữ liệu thật và giữ phong cách phương án A.
- Tìm kiếm, ba kiểu sắp xếp và phân trang hoạt động đúng.
- Chỉ dữ liệu public/active xuất hiện và các aggregate phản ánh database.
- Navigation phân biệt rõ thư viện cộng đồng với thư viện cá nhân.
- Toàn bộ artefact prototype giao diện được loại bỏ, trừ domain abstraction `IPrototype<T>`.
- Test, build và kiểm tra runtime đều thành công.
