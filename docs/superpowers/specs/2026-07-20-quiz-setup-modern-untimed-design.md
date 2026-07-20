# Thiết kế giao diện thiết lập trắc nghiệm hiện đại và chế độ không giới hạn thời gian

**Ngày:** 2026-07-20  
**Nhánh:** `feature/quiz-study-mode`  
**Phạm vi:** Màn hình thiết lập trước khi bắt đầu bài trắc nghiệm và cách hệ thống biểu diễn bài làm không giới hạn thời gian.

## 1. Mục tiêu

- Thay giao diện thiết lập hiện tại còn cứng và phụ thuộc nhiều vào điều khiển mặc định của trình duyệt bằng một giao diện hiện đại, rõ thứ bậc và đồng nhất với khu vực Study.
- Giúp người dùng chọn nhanh thời lượng 5, 10, 15 hoặc 20 phút, nhập thời lượng riêng, hoặc làm bài không giới hạn thời gian.
- Làm nổi bật bài đang dở để người dùng có thể tiếp tục mà không gây nhầm lẫn với hành động bắt đầu bài mới.
- Giữ nguyên flow trắc nghiệm hiện có, chỉ mở rộng trạng thái thời gian và nâng cấp trải nghiệm thiết lập.

## 2. Hướng thiết kế đã chọn

Sử dụng phương án **B1 — thẻ chia hai vùng với lựa chọn không giới hạn nổi bật**.

### 2.1. Bố cục desktop

- Toàn bộ nội dung nằm trong một khối lớn có nền trắng ấm, bo góc rộng, viền mảnh và bóng đổ nhẹ.
- Khi có bài đang dở, khối được chia theo tỷ lệ xấp xỉ 65/35:
  - Bên trái là phần tạo bài mới.
  - Bên phải là thẻ nền tối dành cho bài đang dở, có nút `Tiếp tục làm bài`.
- Khi không có bài đang dở, vùng bên phải được ẩn hoàn toàn; phần tạo bài mới được căn giữa và không để lại khoảng trống.

### 2.2. Bố cục mobile

- Chuyển về một cột.
- Nếu có bài đang dở, thẻ tiếp tục được đặt ở đầu nội dung để người dùng thấy ngay.
- Các lựa chọn thời lượng vẫn đủ lớn để chạm và không tạo cuộn ngang.

### 2.3. Phong cách thị giác

- Hướng hiện đại, tối giản và thân thiện; tránh cảm giác bảng biểu hoặc form quản trị.
- Bảng màu trung tính ấm, chữ đậm rõ ràng, màu cam đất dùng cho hành động chính.
- Biểu tượng và nhãn ngắn giúp người dùng quét nhanh.
- Chuyển động chỉ dùng ở mức nhẹ cho hover, focus và trạng thái chọn; tôn trọng `prefers-reduced-motion`.

## 3. Thành phần giao diện

### 3.1. Phần giới thiệu

- Tiêu đề: `Thiết lập bài trắc nghiệm`.
- Mô tả ngắn cho biết người dùng có thể chọn nhịp độ phù hợp trước khi bắt đầu.
- Có thể hiển thị tên bộ từ và số lượng câu nếu dữ liệu đã có sẵn trong view model; không bổ sung truy vấn chỉ để phục vụ trang trí.

### 3.2. Các mốc thời gian nhanh

- Bốn lựa chọn 5, 10, 15 và 20 phút được trình bày dưới dạng thẻ chọn theo lưới 2x2 trên desktop.
- Mỗi thẻ là một điều khiển radio có vùng bấm bao phủ toàn thẻ.
- Thẻ được chọn có viền, nền và dấu hiệu trạng thái rõ ràng; không dựa riêng vào màu sắc.
- Mốc 10 phút tiếp tục là giá trị mặc định khi mở trang tạo bài mới và chưa có lựa chọn khác.

### 3.3. Không giới hạn thời gian

- Một thẻ chọn riêng, rộng toàn hàng, có biểu tượng vô cực và nhãn `Không giới hạn thời gian`.
- Đây là một lựa chọn ngang hàng với preset và thời lượng tùy chỉnh, không phải nút bắt đầu độc lập.
- Khi được chọn:
  - Bỏ chọn preset hiện tại.
  - Xóa hoặc vô hiệu hóa giá trị thời gian tùy chỉnh phía client.
  - Nút chính đổi nhãn thành `Bắt đầu không giới hạn`.
  - Màn hình làm bài không hiển thị đồng hồ đếm ngược.

### 3.4. Thời lượng tùy chỉnh

- Một hàng nhập gọn gồm nhãn, ô số và hậu tố `phút`.
- Khi người dùng focus hoặc nhập một giá trị hợp lệ, lựa chọn tùy chỉnh trở thành lựa chọn hiện tại và preset/không giới hạn được bỏ chọn.
- Giới hạn hợp lệ tiếp tục là 1–120 phút.
- Lỗi phải xuất hiện gần ô nhập và được liên kết bằng thuộc tính hỗ trợ trình đọc màn hình.

### 3.5. Hành động bắt đầu

- Nút chính nằm gần các lựa chọn thời lượng và có kích thước đủ lớn.
- Nhãn mặc định: `Bắt đầu bài kiểm tra`.
- Nhãn khi không giới hạn được chọn: `Bắt đầu không giới hạn`.
- Khi dữ liệu đang gửi, nút phải ngăn gửi lặp và vẫn có trạng thái focus rõ ràng.

### 3.6. Bài đang dở

- Nếu có `ActiveSessionId`, hiển thị thẻ tương phản ở vùng phải trên desktop hoặc đầu trang trên mobile.
- Thẻ giải thích ngắn rằng tiến độ hiện tại vẫn được giữ.
- Nút `Tiếp tục làm bài` mở lại đúng phiên hiện tại, không tạo phiên mới và không thay đổi thiết lập thời gian của phiên.

## 4. Mô hình trạng thái và dữ liệu gửi lên server

Form phải gửi một lựa chọn thời gian rõ ràng, thuộc đúng một trong ba loại:

1. Preset: 5, 10, 15 hoặc 20 phút.
2. Custom: số phút từ 1 đến 120.
3. Untimed: không giới hạn thời gian.

View model cần có một trường biểu diễn chế độ được chọn, thay vì suy luận hoàn toàn từ radio và ô nhập rời rạc. Tên triển khai có thể là `TimingMode` hoặc tương đương, với các giá trị ổn định cho preset/custom/untimed. Các giá trị phút hiện có tiếp tục được dùng cho preset và custom.

Client-side JavaScript chịu trách nhiệm đồng bộ trạng thái hiển thị, nhưng server là nguồn xác thực cuối cùng. Server phải từ chối các trường hợp:

- Không có chế độ hợp lệ.
- Custom thiếu giá trị hoặc nằm ngoài 1–120 phút.
- Preset không thuộc danh sách cho phép.
- Payload có các giá trị mâu thuẫn; chỉ dữ liệu phù hợp với chế độ đã chọn được sử dụng.

Khi validation thất bại, trang thiết lập được render lại với lựa chọn người dùng vừa nhập và thông báo dễ hiểu.

## 5. Ngữ nghĩa của bài không giới hạn thời gian

Phương án lưu trữ đã chọn không thêm cột hoặc migration mới:

- `QuizStartedAtUtc = NULL`
- `QuizTimeLimitSeconds = NULL`

Hai giá trị thời gian cùng để `NULL` mang nghĩa phiên trắc nghiệm không giới hạn thời gian. Điều này cũng áp dụng cho các phiên cũ đang có hai trường thời gian là `NULL`; chúng được xem là phiên không giới hạn thay vì tự động đổi thành 10 phút.

Đối với phiên không giới hạn:

- Không tính deadline.
- Không hiển thị đồng hồ.
- Không gọi flow nộp bài do hết giờ.
- Người dùng vẫn có thể nộp bài thủ công và sử dụng các điều khiển làm lại/quay lại như bình thường.

## 6. Restart, retry và continue

- `Tiếp tục làm bài` giữ nguyên phiên và trạng thái thời gian hiện tại.
- `Làm lại từ đầu` tạo phiên mới nhưng kế thừa timed/untimed từ phiên nguồn.
- `Làm lại câu sai` và `Làm lại toàn bộ` sau khi hoàn thành cũng kế thừa timed/untimed từ phiên nguồn.
- Phiên có giới hạn giữ nguyên số phút đã chọn ở phiên nguồn.
- Phiên có hai trường thời gian `NULL` tạo phiên kế tiếp không giới hạn.
- Không sử dụng giá trị 0 làm mã thay thế cho không giới hạn.

## 7. Khả năng truy cập

- Mỗi thẻ chọn có label thật liên kết với input; thao tác được bằng bàn phím.
- Trạng thái focus có viền tương phản và không bị loại bỏ.
- Trạng thái chọn không chỉ phân biệt bằng màu; có thêm radio/check/icon hoặc thay đổi viền rõ ràng.
- Vùng bấm tối thiểu phù hợp với thiết bị cảm ứng.
- Thông báo validation dùng cơ chế hiện có của ASP.NET và thuộc tính ARIA phù hợp.
- Chuyển động trang trí được tắt hoặc rút gọn khi người dùng bật reduced motion.

## 8. Kiểm thử

### 8.1. View và style

- Render đúng bố cục split khi có bài đang dở.
- Ẩn vùng bài đang dở và căn lại phần chính khi không có phiên active.
- Có đủ bốn preset, lựa chọn không giới hạn và ô custom.
- Các điều khiển có label, focus style và breakpoint responsive.
- Có rule `prefers-reduced-motion` cho hiệu ứng mới.

### 8.2. Controller/service

- Preset hợp lệ tạo phiên timed với thời điểm bắt đầu và số giây đúng.
- Custom hợp lệ tạo phiên timed đúng giới hạn.
- Untimed tạo phiên với cả hai trường thời gian là `NULL`.
- Untimed không bị đánh dấu hết giờ và không sinh deadline.
- Payload thiếu, sai hoặc mâu thuẫn trả lại trang setup với validation error.
- Restart/retry từ timed giữ nguyên thời lượng.
- Restart/retry từ untimed tiếp tục là untimed.
- Continue không tạo thêm phiên.

### 8.3. Hồi quy

- Flow chọn đáp án, quay lại câu trước, làm lại từ đầu, nộp bài và xem kết quả vẫn hoạt động.
- Các bài timed hiện tại vẫn tự nộp khi hết giờ.
- Chạy toàn bộ test suite của dự án sau khi hoàn tất.

## 9. Phạm vi không thực hiện

- Không thêm migration hoặc cột cơ sở dữ liệu mới.
- Không thay đổi cách chấm điểm, sinh câu hỏi hoặc trộn đáp án.
- Không thiết kế lại toàn bộ màn hình đang làm bài hay màn hình kết quả ngoài những điều chỉnh cần thiết để ẩn timer cho phiên untimed.
- Không thay đổi các chỉnh sửa hover đang tồn tại trong `study-mode-selector.css` và test liên quan; chúng được giữ nguyên như thay đổi độc lập.

## 10. Tiêu chí hoàn thành

- Giao diện setup khớp hướng B1 trên desktop và mobile.
- Người dùng có thể chọn chính xác một trong preset, custom hoặc không giới hạn.
- Phiên untimed hoạt động xuyên suốt start, continue, restart và retry mà không bị hết giờ.
- Phiên timed không bị hồi quy.
- Validation, bàn phím, focus và reduced motion được kiểm chứng.
- Toàn bộ test liên quan và test suite đều vượt qua.
