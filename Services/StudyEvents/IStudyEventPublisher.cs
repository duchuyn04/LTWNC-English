namespace ltwnc.Services.StudyEvents;

// Subject (trạm phát): gửi sự kiện tới mọi IStudyEventObserver đã đăng ký DI.
public interface IStudyEventPublisher
{
    // Gọi lần lượt từng observer; cô lập lỗi thường, nhưng truyền lại cancellation
    Task PublishAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default);
}
