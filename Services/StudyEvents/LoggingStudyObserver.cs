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
        _logger = logger;
    }

    // Tóm tắt sự kiện theo type rồi LogInformation
    public Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
    {
        string summary;

        if (studyEvent is CardProgressChangedEvent cardProgress)
        {
            string learnedText = cardProgress.IsLearned ? "đã thuộc" : "chưa thuộc";
            summary =
                $"User {cardProgress.UserId} cập nhật thẻ {cardProgress.FlashcardId} " +
                $"(bộ {cardProgress.SetId}): {learnedText}";
        }
        else if (studyEvent is StudySessionCompletedEvent sessionCompleted)
        {
            string scorePart = string.Empty;
            if (sessionCompleted.Score.HasValue)
            {
                scorePart = $", điểm {sessionCompleted.Score}";
            }

            summary =
                $"User {sessionCompleted.UserId} hoàn thành buổi {sessionCompleted.Mode} " +
                $"(session {sessionCompleted.SessionId}, bộ {sessionCompleted.SetId}{scorePart})";
        }
        else if (studyEvent is DictationAnswerCheckedEvent dictationAnswer)
        {
            string correctText = dictationAnswer.IsCorrect ? "đúng" : "sai";
            summary =
                $"User {dictationAnswer.UserId} trả lời nghe chép thẻ {dictationAnswer.FlashcardId}: {correctText}";
        }
        else
        {
            summary = $"User {studyEvent.UserId} phát sinh sự kiện {studyEvent.GetType().Name}";
        }

        _logger.LogInformation("Sự kiện học (Observer Logging): {Summary}", summary);
        return Task.CompletedTask;
    }
}
