# Observer, một sự kiện có nhiều người nghe

## Chỉ cần nhớ một ý

Observer cho phép một object phát thông báo khi có việc xảy ra. Nhiều object khác có thể nhận thông báo và tự phản ứng.

Object phát tin không cần biết cụ thể ai đang nghe.

Trong project này:

```text
Người dùng hoàn thành buổi học
              |
              v
      StudyEventPublisher
          /          \
         v            v
Mở huy hiệu       Ghi log
```

## Ví dụ dễ hiểu

Hãy hình dung chuông trường học.

Chuông chỉ phát tín hiệu. Nó không trực tiếp ra lệnh từng học sinh cất sách, từng giáo viên đóng lớp hay bảo vệ mở cổng. Mỗi người nghe chuông rồi tự biết cần làm gì.

`StudyEventPublisher` giống chiếc chuông. `AchievementStudyObserver` và `LoggingStudyObserver` là những người nghe.

## Trước khi áp dụng Observer, code triển khai ra sao?

Trước khi có Observer, các method học kết thúc sau khi lưu dữ liệu:

```csharp
await _context.SaveChangesAsync();
```

Lúc đó chưa có phản ứng mở huy hiệu và ghi log theo sự kiện học.

Nếu thêm các phản ứng bằng cách gọi trực tiếp, service học sẽ có dạng:

```csharp
await _context.SaveChangesAsync();
await _achievementUnlockService.SyncEligibleAsync(userId);
_logger.LogInformation("Người dùng vừa hoàn thành buổi học");
```

Sau này nếu thêm gửi thông báo, cập nhật nhiệm vụ ngày hoặc thống kê khác, service lại phải thêm dependency và lời gọi mới.

Khi đó `StudyService`, `QuizService` và `DictationService` vừa xử lý việc học, vừa phải biết mọi chức năng phụ đang quan tâm đến việc học.

## Vì sao chọn Observer cho chức năng này?

Một hành động học có thể tạo ra nhiều phản ứng độc lập.

Ví dụ, khi hoàn thành buổi học:

- Thành tích kiểm tra xem người dùng có mở huy hiệu mới không.
- Logging ghi lại thông tin hệ thống.
- Một observer khác có thể được thêm sau mà không sửa logic hoàn thành buổi học.

Service học chỉ cần phát một sự kiện. Nó không cần gọi từng chức năng phụ.

```text
Trước Observer:
StudyService gọi trực tiếp AchievementService và Logger.

Sau Observer:
StudyService phát sự kiện. Các observer tự phản ứng.
```

## Observer nằm ở đâu trong project?

| Vai trò GoF | Code |
| --- | --- |
| Subject | [`IStudyEventPublisher`](../Services/StudyEvents/IStudyEventPublisher.cs) |
| Concrete Subject | [`StudyEventPublisher`](../Services/StudyEvents/StudyEventPublisher.cs) |
| Observer | [`IStudyEventObserver`](../Services/StudyEvents/IStudyEventObserver.cs) |
| Concrete Observer | [`AchievementStudyObserver`](../Services/Achievements/AchievementStudyObserver.cs) |
| Concrete Observer | [`LoggingStudyObserver`](../Services/StudyEvents/LoggingStudyObserver.cs) |
| Thông báo | [`StudyEvents.cs`](../Services/StudyEvents/StudyEvents.cs) |
| Nơi phát sự kiện | `StudyService`, `QuizService`, `DictationService`, `EnglishMissionService` |

Interface Observer chỉ yêu cầu một method:

```csharp
public interface IStudyEventObserver
{
    Task OnStudyEventAsync(
        StudyEvent studyEvent,
        CancellationToken cancellationToken = default);
}
```

Publisher giữ danh sách observer do DI cung cấp:

```csharp
foreach (IStudyEventObserver observer in _observers)
{
    await observer.OnStudyEventAsync(studyEvent, cancellationToken);
}
```

## Project có những sự kiện nào?

| Sự kiện | Ý nghĩa |
| --- | --- |
| `CardProgressChangedEvent` | Người dùng đổi trạng thái đã thuộc của một thẻ |
| `StudySessionCompletedEvent` | Người dùng hoàn thành một buổi học |
| `DictationAnswerCheckedEvent` | Một câu trả lời nghe chép đã được chấm |

Sự kiện là một mẩu dữ liệu mô tả điều vừa xảy ra. Ví dụ:

```csharp
await _studyEvents.PublishAsync(new StudySessionCompletedEvent(
    UserId: session.UserId,
    OccurredAtUtc: completedAt,
    SetId: session.FlashcardSetId,
    SessionId: session.Id,
    Mode: session.Mode,
    Score: session.Score));
```

## Vì sao phát sự kiện sau khi lưu database?

Project gọi `SaveChangesAsync()` trước rồi mới publish trong các luồng hiện có: `CardProgressChanged`, `StudySessionCompleted` và `DictationAnswerChecked`.

```text
Lưu kết quả học thành công
            |
            v
Phát sự kiện cho observer
```

Nhờ vậy observer đọc database sẽ thấy dữ liệu mới nhất. Thành tích cũng không được mở dựa trên một buổi học chưa lưu thành công.

## Nếu một Observer bị lỗi hoặc request bị hủy thì sao?

`StudyEventPublisher` gọi từng observer tuần tự và cô lập lỗi thường của từng observer.

Nếu observer thành tích hoặc logging ném exception thông thường:

- Lỗi được ghi log cùng loại observer, loại event và user.
- Observer tiếp theo vẫn được gọi.
- Buổi học đã lưu không bị báo thất bại chỉ vì chức năng phụ lỗi.

Cancellation là nhánh khác. Publisher truyền request token vào observer, kiểm tra token trước/sau mỗi lần gọi, truyền lại `OperationCanceledException` và không gọi observer phía sau khi request đã hủy. Vì vậy không được bắt cancellation như lỗi thường để tiếp tục chain.

Đây là quyết định của project vì thành tích và logging là phản ứng phụ, còn cancellation là tín hiệu điều khiển của caller.

## DI thay cho Attach và Detach

Trong sách GoF, Subject thường có method `Attach()` để đăng ký observer.

Project dùng dependency injection. Các observer được đăng ký trong `Program.cs`, rồi ASP.NET Core đưa danh sách đó vào `StudyEventPublisher`.

Ý nghĩa vẫn giống nhau: publisher có danh sách người nghe, nhưng việc đăng ký diễn ra khi ứng dụng khởi động.

## Kiểm thử

Publisher được kiểm chứng trực tiếp tại [`StudyEventPublisherTests.cs`](../tests/ltwnc.Tests/Services/StudyEvents/StudyEventPublisherTests.cs) với success/order, ordinary failure continuation, contextual error logging, cancellation propagation và suppression của observer phía sau.

## Tự kiểm tra

1. Service học có biết concrete observer nào đang nghe không?
2. Vì sao sự kiện được phát sau khi lưu database?
3. Một observer lỗi có chặn observer khác không?

Đáp án:

1. Không. Service chỉ biết `IStudyEventPublisher`.
2. Để observer nhìn thấy dữ liệu đã được lưu thành công.
3. Không. Publisher bắt lỗi riêng cho từng observer.

## Kết luận ngắn

Observer giúp service học chỉ báo rằng một việc đã xảy ra. Thành tích, logging và các phản ứng khác tự đăng ký để nhận tin mà không làm service học phụ thuộc trực tiếp vào chúng.
