namespace ltwnc.Services.StudyEvents;

// Subject (trạm phát): gửi sự kiện tới mọi IStudyEventObserver đã đăng ký DI.
public interface IStudyEventPublisher
{
    // Gọi lần lượt từng observer; không throw nếu một observer lỗi
    Task PublishAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default);
}
