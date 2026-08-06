# Logic Review hiện tại

> Phạm vi: tính năng ôn tập ngắt quãng tại `/Review`.
> Không bao gồm Content Reports/báo cáo nội dung.
>
> Trạng thái: tài liệu mô tả hiện trạng; chưa áp dụng thay đổi logic.

## 1. Luồng bắt đầu phiên

- Review yêu cầu người dùng đăng nhập.
- `GET /Review`:
  - Nếu user có phiên đang chạy, tiếp tục phiên đó.
  - Nếu không có phiên, chuyển tới danh sách bộ thẻ `/Set`.
- Luồng thực tế hiện tại là `GET /Review/Set/{setId}` và `POST /Review/Set/{setId}/Start`.
- Mỗi user chỉ có một phiên Review đang chạy, không phụ thuộc bộ thẻ.
- Database dùng unique index trên `UserId` cho các phiên chưa `CompletedAtUtc` và chưa `EndedAtUtc`.
- Nếu hai request bắt đầu gần như đồng thời, service bắt lỗi trùng phiên và tải lại phiên thắng cuộc.

## 2. Chọn thẻ theo từng bộ

Khi bắt đầu một bộ thẻ:

1. Kiểm tra bộ thuộc user và không bị `ReviewPaused`.
2. Đọc cài đặt riêng của user + bộ thẻ:
   - Kích thước phiên mặc định: 20 thẻ.
   - Quota thẻ mới mặc định: 5 thẻ/ngày.
   - Khoảng ôn dài hạn tối đa mặc định: 30 ngày.
3. Lấy thẻ đến hạn trước:
   - Thẻ có `ReviewProgress`.
   - `NextReviewAtUtc <= hiện tại`.
   - Sắp xếp theo `NextReviewAtUtc`, `OrderIndex`, rồi `Id`.
   - Số lượng tối đa bằng kích thước phiên.
4. Nếu còn chỗ, lấy thẻ mới:
   - Thẻ chưa có `ReviewProgress`.
   - Không vượt quota thẻ mới của bộ trong ngày.
   - Sắp xếp theo `OrderIndex`, rồi `Id`.
5. Thẻ mới được tính quota tại thời điểm phân vào phiên, không phải lúc đánh giá.
6. Quota dùng ngày theo múi giờ `Asia/Ho_Chi_Minh`.
7. Nếu user kết thúc sớm, thẻ mới đã được phân vẫn tính vào quota trong ngày.
8. Thẻ đến hạn không tiêu hao quota thẻ mới.
9. Bộ bị tạm dừng loại cả thẻ mới và thẻ đến hạn khỏi phiên mới; tiến độ cũ vẫn được giữ.

## 3. Luồng toàn bộ các bộ thẻ còn tồn tại

`ReviewService` vẫn có overload `StartAsync(userId)` cho luồng Review toàn bộ các bộ:

- Lấy thẻ từ tất cả bộ của user, trừ bộ bị tạm dừng.
- Kích thước phiên lấy từ `UserStudySettings` cấp user.
- Thẻ đến hạn được nhóm theo cùng thời điểm đến hạn và xáo trộn trong từng nhóm.
- Thẻ mới dùng quota trên `FlashcardSet.NewCardQuota`.
- Thẻ mới được chọn round-robin giữa các bộ.
- Luồng này hiện không được controller sử dụng để bắt đầu phiên; controller không có UI bắt đầu trực tiếp toàn bộ các bộ.
- Các test/service cũ vẫn đang bao phủ overload này.

## 4. Các trạng thái ghi nhớ

Một `ReviewProgress` được lưu cho mỗi cặp `UserId + FlashcardId`.

Các trạng thái:

- `New`: chưa từng đánh giá.
- `Learning`: đang học ngắn hạn.
- `Reviewing`: đang ôn dài hạn.
- `Relearning`: học lại sau khi quên.

## 5. Lịch theo mức đánh giá

| Trạng thái hiện tại | Again | Hard | Good | Easy |
|---|---|---|---|---|
| `New` | `Learning`, 10 phút | `Learning`, 1 ngày | `Reviewing`, 2 ngày | `Reviewing`, 4 ngày |
| `Learning` | `Learning`, 10 phút | `Learning`, 1 ngày | `Reviewing`, 3 ngày | `Reviewing`, 7 ngày |
| `Reviewing` | `Relearning`, 10 phút | `Reviewing`, `ceil(1.2 × khoảng cũ)` | `Reviewing`, `ceil(2 × khoảng cũ)` | `Reviewing`, `ceil(3 × khoảng cũ)` |
| `Relearning` | `Relearning`, 10 phút | `Relearning`, 1 ngày | `Reviewing`, tối thiểu 1 ngày và `ceil(50% × khoảng cũ)` | `Reviewing`, tối thiểu 2 ngày và `ceil(75% × khoảng cũ)` |

Quy tắc bổ sung:

- Khoảng dài hạn được làm tròn lên.
- Khoảng dài hạn bị giới hạn bởi `ReviewMaxIntervalDays`.
- Giá trị cấu hình tối đa cho phép: 30–365 ngày.
- `Again` không đưa thẻ quay lại ngay trong phiên hiện tại; thẻ được lên lịch cho lần sau.
- Với `Reviewing` và `Relearning`, `LongTermIntervalDays` cũ được giữ lại khi chuyển sang học lại.

## 6. Đánh giá và lưu tiến độ

- Client chỉ mở nút rating sau khi user hiện đáp án.
- Server cũng kiểm tra `answerRevealed`; request chưa hiện đáp án bị từ chối.
- Mỗi `ReviewSessionItem` chỉ nhận rating đầu tiên.
- Nếu request bị gửi lại, service trả kết quả rating đã lưu và không tính lại lịch.
- Khi đánh giá lần đầu:
  - Tạo hoặc cập nhật `ReviewProgress`.
  - Lưu stage trước/sau.
  - Lưu lịch cũ/mới.
  - Lưu thời điểm đánh giá.
- Phiên chỉ hoàn thành khi tất cả thẻ trong phiên đã được đánh giá.
- Đánh giá `Again` vẫn được xem là đã xử lý thẻ trong phiên; thẻ không được chèn lại vào cuối phiên.

## 7. Kết thúc và tiếp tục phiên

- `POST /Review/{sessionId}/End` kết thúc sớm phiên bằng `EndedAtUtc`.
- Các thẻ chưa đánh giá không tạo hoặc cập nhật tiến độ.
- Phiên đã hoàn thành hoặc kết thúc không còn được xem là active.
- Tải lại trang phiên sẽ giữ nguyên danh sách, thứ tự và trạng thái các thẻ đã phân.
- Phiên đang chạy có thể đọc lại từ database sau khi refresh hoặc đổi request.
- Rating của thẻ đã đánh giá không còn hiển thị preview lịch mới.

## 8. Cài đặt Review

Cài đặt chính nằm trong `ReviewSettings`, theo khóa:

```text
UserId + FlashcardSetId
```

Các nhóm cài đặt:

- Số thẻ mỗi phiên.
- Số thẻ mới mỗi ngày.
- Khoảng ôn tối đa.
- Nội dung mặt trước/mặt sau.
- Ảnh: hiện, ẩn, làm mờ, kích thước lớn.
- Phát âm mặt trước/mặt sau.

Nếu bộ chưa có dòng `ReviewSettings`:

1. Copy các giá trị Review hợp lệ từ `UserStudySettings` cũ.
2. Giữ quota riêng của `FlashcardSet` nếu hợp lệ.
3. Giá trị không hợp lệ dùng mặc định an toàn.
4. Lưu một dòng riêng cho user + bộ thẻ.

Cài đặt được chụp vào `SettingsSnapshotJson` khi tạo phiên theo bộ. Vì vậy thay đổi cài đặt trong lúc phiên đang chạy chỉ áp dụng từ phiên kế tiếp.

## 9. Giao diện phiên

- Chỉ hiển thị thẻ chưa đánh giá đầu tiên.
- Hiện đáp án bằng nút hoặc phím `Space`.
- Sau khi hiện đáp án, có thể chọn:
  - `1`: Again/Quên.
  - `2`: Hard/Khó.
  - `3`: Good/Tốt.
  - `4`: Easy/Dễ.
- Sau mỗi rating, controller redirect về phiên để hiển thị thẻ chưa đánh giá tiếp theo.
- Khi hết thẻ, redirect tới trang kết quả.
- Trang kết quả hiển thị số lượng Again/Hard/Good/Easy và danh sách thẻ.

## 10. Quyền truy cập và dữ liệu liên quan

- Controller Review có `[Authorize]`.
- Service luôn kiểm tra user sở hữu bộ thẻ hoặc phiên.
- Các POST có antiforgery token.
- `ReviewProgress` không dùng chung với `UserProgress` của Study/Quiz.
- Xóa thẻ hoặc xóa bộ sẽ dọn tiến độ Review, item phiên và phiên liên quan.
- Copy bộ thẻ không copy tiến độ Review của bộ nguồn.

## 11. Các điểm cần chốt trước khi tinh chỉnh

Hiện có hai bộ quy tắc chọn thẻ khác nhau:

1. Luồng theo từng bộ đang được UI sử dụng.
2. Luồng toàn bộ các bộ vẫn tồn tại trong service/tests.

Ngoài ra, cần quyết định rõ các hành vi sau nếu muốn thay đổi:

- Có giữ mỗi user chỉ có một phiên active hay cho phép mỗi bộ một phiên?
- Thẻ `Again` có quay lại ngay trong cùng phiên không?
- Quota thẻ mới tính khi phân thẻ hay chỉ khi user đánh giá?
- Thẻ đến hạn có cần ưu tiên tuyệt đối hay trộn với thẻ mới?
- Thay đổi cài đặt có áp dụng ngay cho phiên đang chạy không?
- Có giữ lịch hiện tại hay thay bằng công thức spaced repetition khác?

## 12. Quyết định được đề xuất trước tiên

Nên chốt trước rằng Review chính thức chỉ chạy **theo từng bộ** hay vẫn cần một queue chung cho tất cả bộ.

Khuyến nghị hiện tại: giữ luồng **theo từng bộ**, sau đó loại bỏ hoặc đồng nhất overload toàn bộ các bộ để tránh cùng một tính năng nhưng có hai cách tính quota, thứ tự và cài đặt khác nhau.
