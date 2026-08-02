namespace ltwnc.Services.StudyEvents;

// Observer (người nghe) trong mẫu Observer: nhận sự kiện học sau khi service đã lưu DB.
public interface IStudyEventObserver
{
    // Xử lý một sự kiện; lỗi thường được publisher cô lập, cancellation được truyền lại
    Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default);
}
