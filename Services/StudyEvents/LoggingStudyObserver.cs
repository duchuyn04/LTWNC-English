using Microsoft.Extensions.Logging;

namespace ltwnc.Services.StudyEvents;

// Observer log: ghi một dòng mỗi sự kiện (debug + minh họa nhiều listener cùng tin).
// Không ghi DB.
public class LoggingStudyObserver : IStudyEventObserver
{
    // Logger ASP.NET
    private readonly ILogger<LoggingStudyObserver> _logger;

    // Inject logger
    public LoggingStudyObserver(ILogger<LoggingStudyObserver> logger)
    {
        // 1. Lưu dependency `_logger` để các phương thức khác sử dụng.
        _logger = logger;
    }

    // Tóm tắt sự kiện theo type rồi LogInformation
    public Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        // 1. Khai báo `summary` để lưu dữ liệu dùng ở các bước sau.
        string summary;

        // 2. Kiểm tra `studyEvent is CardProgressChangedEvent cardProgress` để chọn nhánh xử lý phù hợp.
        if (studyEvent is CardProgressChangedEvent cardProgress)
        {
            // 3. Tính giá trị và lưu vào `learnedText` để dùng ở bước tiếp theo.
            string learnedText = cardProgress.IsLearned ? "đã thuộc" : "chưa thuộc";
            // 4. Cập nhật `summary` bằng giá trị mới.
            summary =
                $"User {cardProgress.UserId} cập nhật thẻ {cardProgress.FlashcardId} " +
                $"(bộ {cardProgress.SetId}): {learnedText}";
        }
        else if (studyEvent is StudySessionCompletedEvent sessionCompleted)
        {
            // 5. Tính giá trị và lưu vào `scorePart` để dùng ở bước tiếp theo.
            string scorePart = string.Empty;
            // 6. Kiểm tra `sessionCompleted.Score.HasValue` để chọn nhánh xử lý phù hợp.
            if (sessionCompleted.Score.HasValue)
            {
                // 7. Cập nhật `scorePart` bằng giá trị mới.
                scorePart = $", điểm {sessionCompleted.Score}";
            }

            // 8. Cập nhật `summary` bằng giá trị mới.
            summary =
                $"User {sessionCompleted.UserId} hoàn thành buổi {sessionCompleted.Mode} " +
                $"(session {sessionCompleted.SessionId}, bộ {sessionCompleted.SetId}{scorePart})";
        }
        else if (studyEvent is DictationAnswerCheckedEvent dictationAnswer)
        {
            // 9. Tính giá trị và lưu vào `correctText` để dùng ở bước tiếp theo.
            string correctText = dictationAnswer.IsCorrect ? "đúng" : "sai";
            // 10. Cập nhật `summary` bằng giá trị mới.
            summary =
                $"User {dictationAnswer.UserId} trả lời nghe chép thẻ {dictationAnswer.FlashcardId}: {correctText}";
        }
        else
        {
            // 11. Cập nhật `summary` bằng giá trị mới.
            summary = $"User {studyEvent.UserId} phát sinh sự kiện {studyEvent.GetType().Name}";
        }

        // 12. Gọi `LogInformation` để thực hiện bước nghiệp vụ này.
        _logger.LogInformation("Sự kiện học (Observer Logging): {Summary}", summary);
        // 13. Trả `Task.CompletedTask` cho nơi gọi.
        return Task.CompletedTask;
    }
}
