using ltwnc.Models.Entities;

namespace ltwnc.Services.Study;

public sealed class QuizUnavailableException : InvalidOperationException
{
    public QuizUnavailableException(string message) : base(message)
    {
        // 1. Chuyển thông báo không thể tạo Quiz cho lớp Exception cơ sở.
    }
}

public sealed record QuizPoolAvailability(
    bool IsAvailable,
    int DistinctTermCount,
    int DistinctDefinitionCount,
    string? UnavailableReason);

public sealed class QuizSetupState
{
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public StudySession? ActiveSession { get; init; }
}

public sealed class QuizQuestionState
{
    public int SessionId { get; init; }
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public int TotalQuestions { get; init; }
    public int AnsweredCount { get; init; }
    public int CorrectCount { get; init; }
    public DateTime? DeadlineUtc { get; init; }
    public int? RemainingSeconds { get; init; }
    public QuizSessionQuestion? Question { get; init; }
    public bool IsReviewOnly { get; init; }
    public int? SelectedChoiceIndex { get; init; }
    public int? CorrectChoiceIndex { get; init; }
    public bool? IsCorrect { get; init; }
    public int? PreviousQuestionId { get; init; }
    public int? NextQuestionId { get; init; }
    public int? CurrentPendingQuestionId { get; init; }
    public bool IsComplete => Question is null;
}

public sealed record QuizAnswerResult(
    bool IsCorrect,
    int CorrectChoiceIndex,
    bool IsLastQuestion);

public sealed class QuizConflictException : InvalidOperationException
{
    public QuizConflictException(string message) : base(message)
    {
        // 1. Chuyển thông báo xung đột phiên Quiz cho lớp Exception cơ sở.
    }
}

public sealed class QuizSessionAbandonedException : InvalidOperationException
{
    public QuizSessionAbandonedException(int? activeSessionId)
        : base("Phiên trắc nghiệm này đã được thay thế.")
    {
        // 1. Lưu dependency `ActiveSessionId` để các phương thức khác sử dụng.
        ActiveSessionId = activeSessionId;
    }

    public int? ActiveSessionId { get; }
}

public sealed class QuizNotExpiredException : InvalidOperationException
{
    public QuizNotExpiredException(int remainingSeconds)
        : base("Phiên trắc nghiệm chưa hết thời gian.")
    {
        // 1. Lưu dependency `RemainingSeconds` để các phương thức khác sử dụng.
        RemainingSeconds = remainingSeconds;
    }

    public int RemainingSeconds { get; }
}

public sealed class QuizExpiredException : InvalidOperationException
{
    public QuizExpiredException() : base("Phiên trắc nghiệm đã hết thời gian.")
    {
        // 1. Khởi tạo lỗi hết thời gian bằng thông báo cố định từ lớp cơ sở.
    }
}

public sealed class QuizSessionResult
{
    public int SessionId { get; init; }
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public int TotalQuestions { get; init; }
    public int CorrectCount { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<QuizWrongAnswer> WrongAnswers { get; init; } =
        Array.Empty<QuizWrongAnswer>();
}

public sealed record QuizWrongAnswer(
    int FlashcardId,
    QuizQuestionDirection Direction,
    string PromptText,
    string SelectedAnswer,
    string CorrectAnswer);
