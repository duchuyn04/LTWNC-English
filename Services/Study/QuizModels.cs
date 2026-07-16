using ltwnc.Models.Entities;

namespace ltwnc.Services.Study;

public sealed class QuizUnavailableException : InvalidOperationException
{
    public QuizUnavailableException(string message) : base(message) { }
}

public sealed record QuizPoolAvailability(
    bool IsAvailable,
    int DistinctTermCount,
    int DistinctDefinitionCount,
    string? UnavailableReason);

public sealed class QuizQuestionState
{
    public int SessionId { get; init; }
    public int SetId { get; init; }
    public string SetTitle { get; init; } = string.Empty;
    public int TotalQuestions { get; init; }
    public int AnsweredCount { get; init; }
    public int CorrectCount { get; init; }
    public QuizSessionQuestion? Question { get; init; }
    public bool IsComplete => Question is null;
}

public sealed record QuizAnswerResult(
    bool IsCorrect,
    int CorrectChoiceIndex,
    bool IsLastQuestion);

public sealed class QuizConflictException : InvalidOperationException
{
    public QuizConflictException(string message) : base(message) { }
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
