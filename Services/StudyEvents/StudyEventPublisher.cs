using Microsoft.Extensions.Logging;

namespace ltwnc.Services.StudyEvents;

// ============================================================
// SUBJECT cụ thể: trạm phát sự kiện học tập (mẫu Observer).
//
// Cách hoạt động bằng lời thường:
// 1. Khi app khởi động, hệ thống đăng ký sẵn danh sách "người theo dõi"
//    (ví dụ: người mở thành tích, người ghi log).
// 2. Mỗi lần có sự kiện, trạm phát gõ cửa từng người theo dõi.
// 3. Nếu một người theo dõi bị lỗi, các người khác vẫn nhận tin
//    — việc học của user không bị hỏng vì lỗi phụ (thành tích/log).
//
// Trong ASP.NET, danh sách observer lấy từ DI (hộp đăng ký dịch vụ),
// tương đương việc "subscribe" trong sách GoF.
// ============================================================
public class StudyEventPublisher : IStudyEventPublisher
{
    // Danh sách tất cả "người theo dõi" đã đăng ký
    private readonly IEnumerable<IStudyEventObserver> _observers;

    // Ghi nhật ký lỗi nếu một observer xử lý hỏng
    private readonly ILogger<StudyEventPublisher> _logger;

    public StudyEventPublisher(
        IEnumerable<IStudyEventObserver> observers,
        ILogger<StudyEventPublisher> logger)
    {
        _observers = observers;
        _logger = logger;
    }

    // Báo tin cho từng observer, lần lượt, không dừng cả chuỗi nếu một người lỗi
    public async Task PublishAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnStudyEventAsync(studyEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                // Lỗi phụ (thành tích/log) không được làm hỏng buổi học chính
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
