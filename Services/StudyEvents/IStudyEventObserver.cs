namespace ltwnc.Services.StudyEvents;

// Observer (người nghe) trong mẫu Observer: nhận sự kiện học sau khi service đã lưu DB.
public interface IStudyEventObserver
{
    // Xử lý một sự kiện; lỗi sẽ bị publisher bắt để không làm hỏng listener khác
    Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default);
}
