# Thiết kế giao diện Focus Deck cho ôn tập ngắt quãng

## Mục tiêu

Áp dụng ngôn ngữ giao diện của Prototype A — Focus Deck cho toàn bộ luồng ôn tập ngắt quãng tại `/Review`, gồm trang bắt đầu, phiên ôn và trang kết quả. Thay đổi chỉ tác động phần trình bày; lịch ôn, trạng thái thẻ và các endpoint hiện tại được giữ nguyên.

## Phạm vi

- Thiết kế lại `Views/Review/Index.cshtml`.
- Thiết kế lại `Views/Review/Session.cshtml`.
- Thiết kế lại `Views/Review/Result.cshtml`.
- Dùng một stylesheet riêng cho ba màn hình Review.
- Không thay đổi controller, service, entity, migration hoặc database.
- Không thêm thư viện frontend.

## Ngôn ngữ hình ảnh

Luồng Review chạy trong chế độ tập trung và ẩn navbar/footer chung. Giao diện dùng nền kem, chữ xanh đen, điểm nhấn đồng, đường viền mảnh và khoảng trắng rộng như Prototype A. Nội dung chính giới hạn khoảng 900px để flashcard là trọng tâm. Font và design token hiện có trong `site.css` tiếp tục được sử dụng.

## Màn hình bắt đầu

Trang `/Review` dùng cùng khung Focus Deck:

- Header nhỏ với liên kết quay lại bộ thẻ, nhãn “Ôn tập ngắt quãng” và trạng thái sẵn sàng.
- Khối giới thiệu ngắn giải thích thẻ đến hạn được ưu tiên trước thẻ mới.
- Một panel trung tâm có CTA “Bắt đầu ôn tập”.
- `TempData["Message"]` vẫn hiển thị bằng vùng trạng thái có `role="status"`.
- Form POST `/Review/Start` và anti-forgery token được giữ nguyên.

## Màn hình phiên ôn

Trang `/Review/{sessionId}` bám sát bố cục Prototype A:

- Header gồm nút thoát/kết thúc sớm, tên bộ thẻ và chỉ số thẻ hiện tại.
- Thanh tiến độ được tính từ `RatedCards / TotalCards`.
- Flashcard lớn hiển thị mặt trước theo `StudySettingsViewModel`; ảnh, IPA và các tùy chọn hiện có vẫn được tôn trọng.
- Sau khi người dùng bấm “Hiện đáp án”, vùng đáp án xuất hiện và bốn mức nhớ được bật.
- Bốn mức giữ nguyên nghiệp vụ hiện tại: Again, Hard, Good, Easy và thời gian dự kiến từ `RatingPreviews`.
- Mỗi nút mức nhớ vẫn submit form POST `/Review/{sessionId}/Rate` với anti-forgery token, `flashcardId`, `answerRevealed` và `rating`.
- Phím `Space` hiện đáp án; phím `1`, `2`, `3`, `4` submit lần lượt Again, Hard, Good, Easy sau khi đáp án đã hiện.
- Kết thúc sớm vẫn POST tới `/Review/{sessionId}/End` và yêu cầu xác nhận.

## Màn hình kết quả

Trang `/Review/{sessionId}/Result` dùng cùng khung và màu sắc:

- Header thể hiện lượt đã hoàn thành hay kết thúc sớm.
- Thanh tiến độ và số thẻ đã đánh giá.
- Bốn chỉ số tổng kết Again, Hard, Good, Easy.
- Danh sách thẻ hiển thị từ, nghĩa và trạng thái sau lượt ôn.
- CTA quay lại `/Review` để bắt đầu lượt tiếp theo.

## Cấu trúc triển khai

Dùng ba Razor view hiện có và một stylesheet dùng chung. Không tạo partial hoặc component mới vì markup mỗi màn hình ngắn và không có khối tương tác đủ phức tạp để biện minh cho abstraction riêng. JavaScript chỉ nằm trong `Session.cshtml`, phục vụ hiện đáp án và phím tắt.

## Responsive và khả năng truy cập

- Desktop giữ khung flashcard rộng, cân đối như Prototype A.
- Mobile thu gọn header, cho nhóm nút đánh giá xuống lưới hai cột và tránh tràn chữ dài.
- Các form, heading, `role="group"`, `aria-label`, vùng trạng thái và trạng thái `disabled` vẫn rõ ràng cho trình đọc màn hình.
- Focus state phải hiển thị; chuyển động bị tắt khi người dùng bật `prefers-reduced-motion`.
- Không dùng màu làm tín hiệu duy nhất: mỗi mức nhớ luôn có nhãn chữ.

## Xử lý lỗi và trạng thái biên

- Trang bắt đầu hiển thị thông báo nghiệp vụ từ `TempData`.
- Session chỉ render flashcard khi model có thẻ; hành vi fallback hiện tại được giữ nguyên.
- Tiến độ tránh chia cho 0 khi `TotalCards` bằng 0.
- Nội dung dài và ảnh lớn phải co trong flashcard, không phá layout.
- JavaScript không thay thế validation phía server; nó chỉ điều khiển trạng thái giao diện.

## Kiểm thử

- Build Razor/C# bằng `dotnet build`.
- Chạy các test Review hiện có nếu project test khả dụng.
- Smoke test Playwright với tài khoản thật: đăng nhập, mở `/Review`, bắt đầu lượt, hiện đáp án, chọn một mức nhớ, chuyển thẻ, kết thúc sớm và kiểm tra trang kết quả.
- Kiểm tra desktop và viewport mobile.
- Kiểm tra console và server log không có lỗi mới.

## Tiêu chí hoàn thành

- Cả ba màn hình Review có cùng ngôn ngữ Focus Deck của Prototype A.
- Luồng POST, anti-forgery, lịch ôn và dữ liệu kết quả hoạt động như trước.
- Bốn mức nhớ và thời gian dự kiến hiển thị chính xác.
- Giao diện dùng được trên desktop/mobile và bằng bàn phím.
- Không có thay đổi ngoài phạm vi Review UI.
