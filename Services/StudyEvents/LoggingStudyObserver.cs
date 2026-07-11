using Microsoft.Extensions.Logging;

namespace ltwnc.Services.StudyEvents;

// ============================================================
// OBSERVER cụ thể #2: "người theo dõi ghi nhật ký".
//
// Mục đích kép:
// 1. Ghi lại trên log hệ thống mỗi khi có sự kiện học (dễ debug).
// 2. Chứng minh mẫu Observer: MỘT sự kiện, NHIỀU người nghe độc lập
//    (AchievementStudyObserver làm việc khác, class này chỉ ghi log).
//
// Không lưu database — chỉ in ra log của ứng dụng.
// ============================================================
public class LoggingStudyObserver : IStudyEventObserver
{
    private readonly ILogger<LoggingStudyObserver> _logger;

    public LoggingStudyObserver(ILogger<LoggingStudyObserver> logger)
    {
        _logger = logger;
    }

    // Mỗi sự kiện học → một dòng log dễ đọc
    public Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        var summary = studyEvent switch
        {
            CardProgressChangedEvent e =>
                $"User {e.UserId} cập nhật thẻ {e.FlashcardId} (bộ {e.SetId}): " +
                (e.IsLearned ? "đã thuộc" : "chưa thuộc"),

            StudySessionCompletedEvent e =>
                $"User {e.UserId} hoàn thành buổi {e.Mode} (session {e.SessionId}, bộ {e.SetId}" +
                (e.Score.HasValue ? $", điểm {e.Score}" : "") + ")",

            DictationAnswerCheckedEvent e =>
                $"User {e.UserId} trả lời nghe chép thẻ {e.FlashcardId}: " +
                (e.IsCorrect ? "đúng" : "sai"),

            _ => $"User {studyEvent.UserId} phát sinh sự kiện {studyEvent.GetType().Name}"
        };

        _logger.LogInformation("Sự kiện học (Observer Logging): {Summary}", summary);
        return Task.CompletedTask;
    }
}
