# Thiết kế chế độ Trắc nghiệm trong Study

**Ngày:** 2026-07-16

**Trạng thái:** Đã duyệt

## 1. Mục tiêu

Biến `StudyMode.Quiz` từ mục “Sắp ra mắt” thành một chế độ học hoàn chỉnh trong Study Hub. Một lượt Trắc nghiệm hỏi toàn bộ thẻ phù hợp với bộ lọc hiện tại, trộn hai dạng câu hỏi Anh → Việt và Việt → Anh, có bốn lựa chọn, chấm ngay từng câu, lưu phiên phía máy chủ và cho phép làm lại các câu sai.

Kết quả Quiz chỉ ghi nhận điểm của phiên học. Quiz không tự thay đổi `UserProgress`, `IsLearned` hoặc trạng thái thuộc/chưa thuộc của thẻ.

## 2. Phạm vi

### Bao gồm

- Thêm Quiz vào danh sách mode thật trên Study Hub qua Strategy.
- Tạo, tiếp tục và hoàn thành phiên Quiz phía máy chủ.
- Tạo một câu cho mỗi thẻ phù hợp với bộ lọc Study Hub.
- Phân bổ gần cân bằng hai chiều hỏi và xáo trộn thứ tự câu.
- Tạo đúng bốn lựa chọn có nội dung phân biệt.
- Dùng đáp án nhiễu trong cùng bộ trước, sau đó từ các bộ khác thuộc thư viện của người dùng.
- Chấm ngay từng câu, lưu câu trả lời một lần và tính điểm phía máy chủ.
- Trang kết quả có danh sách câu sai, làm lại câu sai, làm lại toàn bộ và quay về Study Hub.
- Migration, kiểm thử service/controller/view và cập nhật tài liệu liên quan.

### Không bao gồm

- Cập nhật tiến trình thuộc thẻ dựa trên kết quả Quiz.
- Giới hạn số câu hoặc màn hình chọn 5/10/20 câu.
- Hẹn giờ, bảng xếp hạng, lịch sử Quiz riêng hoặc thành tích dành riêng cho Quiz.
- Dùng đáp án nhiễu từ bộ thẻ của người dùng khác.
- Cho phép làm Quiz khi chưa đăng nhập.

## 3. Quy tắc nghiệp vụ

1. Câu hỏi chỉ lấy từ bộ đang học và phải tuân theo `StarredOnly`/`UnlearnedOnly` hiện tại.
2. Mỗi thẻ phù hợp xuất hiện đúng một lần trong lượt Quiz đầy đủ.
3. Với từ hai câu trở lên, số câu Anh → Việt và Việt → Anh chênh nhau tối đa một. Chiều nhận câu dư được chọn ngẫu nhiên. Với một câu, chiều hỏi được chọn ngẫu nhiên.
4. Anh → Việt hiển thị `FrontText` làm đề và dùng `BackText` cho các lựa chọn. Việt → Anh làm ngược lại.
5. Bốn nội dung lựa chọn phải phân biệt sau khi trim và so sánh không phân biệt hoa thường. Lựa chọn đúng xuất hiện đúng một lần.
6. Đáp án nhiễu được lấy theo thứ tự ưu tiên: thẻ khác trong bộ hiện tại, rồi thẻ thuộc các bộ có `UserId` bằng người dùng hiện tại. Không dùng bộ của người dùng khác chỉ vì bộ đó public.
7. Bộ lọc Study Hub không áp dụng lên nguồn đáp án nhiễu; nó chỉ quyết định thẻ nào trở thành câu hỏi.
8. Vì phiên trộn cả hai chiều, Quiz chỉ khả dụng khi tập nguồn có ít nhất bốn `FrontText` phân biệt và bốn `BackText` phân biệt. Nếu không đạt, Study Hub hiển thị lý do thiếu dữ liệu.
9. Câu trả lời đã chấm không được thay đổi. Gửi lại cùng lựa chọn là thao tác idempotent; gửi lựa chọn khác trả xung đột.
10. Điểm là `round(correct / total * 100)` dưới dạng số nguyên và chỉ được tính từ dữ liệu đã lưu trên server.
11. Làm lại câu sai tạo một `StudySession` mới chỉ từ các thẻ sai, giữ chiều hỏi ban đầu, đồng thời xáo trộn lại thứ tự câu và vị trí đáp án.
12. Phiên cũ không bị sửa hoặc xóa khi tạo phiên làm lại.

## 4. Kiến trúc

### `QuizModeStrategy`

Thêm `QuizModeStrategy : IStudyModeStrategy` với `Mode = StudyMode.Quiz`.

- Lấy tập câu hỏi bằng `IStudyCardQueryService`, giống Flashcard và Dictation, để Study Hub và màn Quiz dùng cùng quy tắc lọc.
- Kiểm tra khả năng tạo bốn lựa chọn từ bộ hiện tại cộng các bộ thuộc người dùng.
- Xây `StudyModeOptionViewModel` với tên “Trắc nghiệm”, icon `ph-question`, URL `/Study/{setId}/Quiz`, số câu và thời gian ước lượng khoảng 30 giây/câu.
- Khi strategy được đăng ký DI, `StudyService` tự bỏ Quiz khỏi `RoadmapModes` theo cơ chế hiện có.

Để strategy có thể tính availability bằng dữ liệu thư viện của người dùng mà không giữ mutable state trong scoped service, contract xây option sẽ trở thành bất đồng bộ và nhận `userId`, ví dụ `BuildOptionAsync(setId, cards, settings, userId)`. Hai strategy hiện có trả kết quả trực tiếp qua `Task.FromResult`; `StudyService` await contract mới. Đây là thay đổi contract có chủ đích, giới hạn trong tầng StudyModes và các test tương ứng.

### `IQuizService` / `QuizService`

`QuizService` là nơi duy nhất thực hiện nghiệp vụ phiên Quiz:

- Tạo hoặc tìm phiên đang làm.
- Dựng câu hỏi và đáp án nhiễu.
- Lấy câu chưa trả lời tiếp theo.
- Chấm và lưu câu trả lời.
- Hoàn thành phiên và phát `StudySessionCompletedEvent` đúng một lần.
- Đọc kết quả.
- Tạo phiên làm lại câu sai hoặc làm lại toàn bộ.

Controller không tự trộn, tự chấm hoặc tin điểm do client gửi lên.

### `StudyController`

Thêm các action Quiz vào `StudyController` để giữ URL và authorization nhất quán với Flashcard/Dictation. Controller nhận `IQuizService`, lấy `ICurrentUser.UserId`, chuyển exception nghiệp vụ thành HTTP/View phù hợp và map domain result sang view model.

## 5. Mô hình dữ liệu

### `QuizQuestionDirection`

Enum gồm:

- `TermToDefinition`
- `DefinitionToTerm`

### `QuizSessionQuestion`

Một dòng được tạo trước cho mỗi câu hỏi:

- `Id`
- `StudySessionId`
- `FlashcardId`
- `OrderIndex`
- `Direction`
- `PromptText`
- `Choice1Text`, `Choice2Text`, `Choice3Text`, `Choice4Text`
- `CorrectChoiceIndex` (0–3)
- `SelectedChoiceIndex` nullable (0–3)
- `IsCorrect` nullable
- `AnsweredAt` nullable

Nội dung đề và lựa chọn là snapshot để một lần chỉnh sửa thẻ sau đó không làm thay đổi đề cũ hoặc màn kết quả. `FlashcardId` vẫn được giữ để truy vết thẻ nguồn và tạo phiên làm lại.

Ràng buộc/index:

- Index trên `StudySessionId` để tải phiên nhanh.
- Unique `(StudySessionId, OrderIndex)`.
- Unique `(StudySessionId, FlashcardId)` vì mỗi thẻ chỉ được hỏi một lần trong một phiên.
- Quan hệ tới `StudySession` dùng cascade delete.
- Quan hệ tới `Flashcard` theo cách xử lý lịch sử hiện có của project; migration và test SQLite phải xác nhận hành vi xóa phù hợp, không âm thầm mất chi tiết phiên.

`StudySession` tiếp tục là bản ghi phiên tổng quát:

- `Mode = StudyMode.Quiz`.
- `Score = null` trong lúc chưa hoàn thành.
- `Score` được gán khi câu cuối đã trả lời.
- `CompletedAt` được cập nhật khi hoàn thành.

Không cần thêm bảng lựa chọn riêng vì số lựa chọn cố định là bốn và snapshot bốn cột làm validation/chấm điểm đơn giản, rõ ràng.

## 6. Luồng tạo và tiếp tục phiên

### Bắt đầu từ Study Hub

1. `GET /Study/{setId}/Quiz` kiểm tra người dùng và quyền học bộ thẻ.
2. Nếu có phiên Quiz cùng user/set với `Score = null` và còn câu chưa trả lời, redirect tới phiên đó để tiếp tục.
3. Nếu không có, service lấy thẻ câu hỏi qua `QuizModeStrategy` và kiểm tra pool đáp án.
4. Nếu không có câu hỏi hoặc không đủ dữ liệu tạo bốn đáp án, redirect về Study Hub kèm thông báo rõ ràng.
5. Service tạo `StudySession`, tạo toàn bộ `QuizSessionQuestion` trong một transaction, rồi redirect tới `/Study/{setId}/Quiz/{sessionId}`.

### Hiển thị câu hiện tại

`GET /Study/{setId}/Quiz/{sessionId}` xác minh chủ phiên và bộ thẻ, rồi lấy câu chưa trả lời có `OrderIndex` nhỏ nhất. View model không chứa `CorrectChoiceIndex`. Nếu không còn câu chưa trả lời, action redirect tới kết quả; service bảo đảm phiên đã được hoàn thành.

### Trả lời

`POST /Study/{setId}/Quiz/{sessionId}/Answer` nhận `questionId` và `selectedChoiceIndex`:

1. Xác minh anti-forgery token.
2. Xác minh session thuộc user, đúng set, `Mode = Quiz`, chưa hoàn thành.
3. Xác minh question thuộc session và index nằm trong 0–3.
4. Nếu đã trả lời cùng lựa chọn, trả lại kết quả đã lưu. Nếu lựa chọn khác, trả `409 Conflict`.
5. So sánh với `CorrectChoiceIndex`, lưu lựa chọn, đúng/sai và `AnsweredAt`.
6. Nếu là câu cuối, tính `Score`, cập nhật `CompletedAt` và phát `StudySessionCompletedEvent` một lần.
7. Trả JSON gồm `isCorrect`, `correctChoiceIndex`, `isLastQuestion` và URL tiếp theo/kết quả.

`QuizService` không ghi `UserProgress` và không phát sự kiện thay đổi tiến trình thẻ.

## 7. Làm lại

### Làm lại câu sai

`POST /Study/{setId}/Quiz/{sessionId}/RetryWrong`:

- Chỉ chấp nhận phiên đã hoàn thành thuộc người dùng.
- Lấy các dòng `IsCorrect = false`.
- Nếu không có câu sai, redirect lại kết quả.
- Tạo phiên mới với cùng `FlashcardId` và `Direction` cho từng câu sai.
- Dựng lại bốn lựa chọn từ pool hiện tại, xáo trộn câu và vị trí lựa chọn.
- Redirect tới phiên mới.

Nếu một thẻ nguồn không còn tồn tại hoặc pool hiện tại không còn đủ đáp án, service không tạo phiên nửa chừng; transaction rollback và UI hiển thị lý do.

### Làm lại toàn bộ

`POST /Study/{setId}/Quiz/{sessionId}/RetryAll` tạo phiên mới từ toàn bộ thẻ của phiên cũ. Việc này giúp “làm lại” giữ nguyên phạm vi ban đầu ngay cả khi bộ lọc Study Hub đã đổi sau đó. Hai chiều hỏi có thể được phân bổ lại và toàn bộ lựa chọn được xáo trộn mới.

## 8. Giao diện

### Study Hub

- Quiz xuất hiện cùng Flashcard và Nghe chép trong `Modes`.
- Mode card hiển thị số câu, thời gian dự kiến và trạng thái đề xuất theo cơ chế hiện có.
- Quiz chưa thay đổi thuật toán recommendation hiện tại: Flashcard/Dictation vẫn là hai mode tự động được đề xuất. Quiz chỉ là lựa chọn chủ động trong phạm vi này.
- Nếu unavailable, card bị khóa và hiển thị lý do: không có câu theo bộ lọc hoặc thư viện chưa đủ bốn từ/nghĩa phân biệt.

### Màn câu hỏi

- Tên bộ thẻ, `Câu X / N`, số câu đúng hiện tại và liên kết thoát về Study Hub.
- Nhãn “Chọn nghĩa tiếng Việt” hoặc “Chọn từ tiếng Anh”.
- Nội dung đề lớn ở trung tâm và bốn nút lựa chọn.
- Khi chọn, JavaScript khóa bốn nút trong lúc gửi request để chống nhấp đúp.
- Sau response, đáp án đúng màu xanh; lựa chọn sai màu đỏ; thông báo đúng/sai được đặt trong vùng có `aria-live`.
- Nút “Câu tiếp theo” tải cùng URL phiên để server trả câu chưa làm tiếp theo. Ở câu cuối, nút là “Xem kết quả”.
- Khi request lỗi, mở lại nút khi an toàn và hiển thị thông báo; không tự đoán kết quả ở client.

### Màn kết quả

- Điểm phần trăm và số đúng/tổng số.
- Danh sách câu sai với chiều hỏi, đề, lựa chọn của user và đáp án đúng.
- “Làm lại câu sai” chỉ hiện khi có câu sai.
- “Làm lại toàn bộ” và “Về Study Hub” luôn có.

## 9. Lỗi và bảo mật

- `404 Not Found`: set, session hoặc question không tồn tại/không khớp route.
- `403 Forbidden`: phiên không thuộc người dùng hiện tại hoặc user không có quyền học set.
- `400 Bad Request`: index lựa chọn ngoài 0–3 hoặc payload không hợp lệ.
- `409 Conflict`: cố thay câu trả lời đã chấm hoặc thao tác trên phiên đã hoàn thành.
- POST dùng anti-forgery token.
- Razor encoding được giữ cho toàn bộ snapshot text; JavaScript dùng `textContent`, không chèn dữ liệu học bằng `innerHTML`.
- Client không nhận đáp án đúng trước khi trả lời và không gửi điểm cuối phiên.
- Tạo session/questions và tạo retry session chạy trong transaction để không để lại phiên thiếu câu.
- Complete phải idempotent: event hoàn thành chỉ phát ở lần chuyển từ chưa hoàn thành sang hoàn thành.

## 10. Kiểm thử

### Strategy

- Quiz dùng đúng bộ lọc chung.
- Option có đúng metadata, URL, số câu và thời gian.
- Có câu nhưng pool thiếu term hoặc definition phân biệt thì unavailable với lý do đúng.
- Đăng ký `QuizModeStrategy` làm Quiz biến mất khỏi roadmap mà không thêm switch riêng cho mode active.

### Service

- Mỗi thẻ phù hợp tạo đúng một câu.
- Hai direction chênh tối đa một và thứ tự có thể xáo trộn.
- Mỗi câu có đúng bốn lựa chọn phân biệt, đáp án đúng đúng một lần.
- Distractor ưu tiên cùng set rồi mới dùng set khác thuộc user.
- Không lấy distractor từ set của user khác.
- Thiếu pool thì transaction không để lại session dở.
- Chấm đúng/sai và lưu snapshot lựa chọn chính xác.
- Gửi lại cùng đáp án idempotent; đổi đáp án bị từ chối.
- User khác không đọc/chấm/retry được phiên.
- Câu cuối tính đúng điểm, hoàn thành và publish event một lần.
- Quiz không tạo hoặc sửa `UserProgress`.
- Retry wrong chỉ chứa thẻ sai, giữ direction và không sửa session gốc.
- Retry all giữ tập thẻ gốc.

### Controller và view

- Start tạo mới hoặc resume đúng phiên chưa hoàn thành.
- Không đủ dữ liệu quay về Hub với message.
- Route question/result kiểm tra set/session/user.
- Answer map đúng mã `400/403/404/409` và JSON contract.
- Markup có bốn lựa chọn, progress, direction label, anti-forgery token và vùng `aria-live`.
- JavaScript khóa lựa chọn, tô đúng/sai từ response và điều hướng đúng ở câu cuối.
- Result hiển thị câu sai và ẩn retry-wrong khi đạt 100%.

### Persistence và regression

- SQLite test xác nhận unique index, foreign key và transaction behavior.
- Migration SQL Server tạo schema đúng.
- Chạy toàn bộ `dotnet test` và `dotnet build` để bảo đảm Flashcard, Dictation, Study Hub và achievements hiện có không bị ảnh hưởng.

## 11. Tiêu chí hoàn thành

- Người dùng đăng nhập có thể mở Quiz từ Study Hub và làm toàn bộ thẻ theo bộ lọc.
- Mỗi câu trộn một trong hai chiều hỏi, có đúng bốn lựa chọn và được chấm ngay trên server.
- Reload tiếp tục phiên mà không đổi đề hoặc mất câu đã trả lời.
- Điểm cuối phiên, danh sách câu sai và các thao tác làm lại hoạt động đúng.
- Quiz không thay đổi trạng thái thuộc của thẻ.
- Truy cập chéo user, sửa đáp án đã chấm và giả mạo điểm đều bị ngăn chặn.
- Migration, build và toàn bộ test suite đều thành công.
