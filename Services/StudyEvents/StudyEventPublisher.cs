using Microsoft.Extensions.Logging;

namespace ltwnc.Services.StudyEvents;

// Subject cụ thể: phát sự kiện học cho mọi observer.
// Observer lấy từ DI lúc startup (tương đương Attach trong sách GoF).
// Một observer lỗi chỉ log, không dừng chuỗi và không phá buổi học.
public class StudyEventPublisher : IStudyEventPublisher
{
    // Danh sách observer đã AddScoped trong Program.cs
    private readonly IEnumerable<IStudyEventObserver> _observers;

    // Log khi một observer throw
    private readonly ILogger<StudyEventPublisher> _logger;

    // Inject observers + logger
    public StudyEventPublisher(
        IEnumerable<IStudyEventObserver> observers,
        ILogger<StudyEventPublisher> logger)
    {
        // 1. Lưu dependency `_observers` để các phương thức khác sử dụng.
        _observers = observers;
        // 2. Lưu dependency `_logger` để các phương thức khác sử dụng.
        _logger = logger;
    }

    // Gọi OnStudyEventAsync từng observer; catch từng cái
    public async Task PublishAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        // 1. Duyệt từng `observer` trong `_observers` để xử lý lần lượt.
        foreach (IStudyEventObserver observer in _observers)
        {
            // 2. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
            try
            {
                // 3. Gọi `OnStudyEventAsync` để thực hiện bước nghiệp vụ này.
                await observer.OnStudyEventAsync(studyEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                // Thành tích / log hỏng không được rollback buổi học đã Save
                // 4. Gọi `LogError` để thực hiện bước nghiệp vụ này.
                _logger.LogError(
                    ex,
                    "Observer {ObserverType} lỗi khi xử lý sự kiện {EventType} của user {UserId}.",
                    observer.GetType().Name,
                    studyEvent.GetType().Name,
                    studyEvent.UserId);
            }
        }
    }
}
