namespace ltwnc.Services.Lessons;

public sealed record LessonListItem(
    int Id,
    string Title,
    string? Summary,
    string Status,
    int SortOrder,
    DateTime UpdatedAtUtc,
    int QuestionCount);

public sealed record LessonDetail(
    int Id,
    string Title,
    string? Summary,
    string ContentMarkdown,
    string ContentHtml,
    string Status,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int QuestionCount);

public sealed record LessonSaveCommand(
    int? Id,
    string Title,
    string? Summary,
    string ContentMarkdown,
    string Status,
    int? SortOrder,
    string? ActorUserId);

public sealed record LessonSaveResult(
    bool Succeeded,
    string? Error = null,
    int? LessonId = null);

public sealed record LessonQuestionAdminItem(
    int Id,
    string Type,
    string Prompt,
    int SortOrder,
    IReadOnlyList<string> Options,
    int? CorrectOptionIndex,
    IReadOnlyList<string> AcceptedAnswers);

public sealed record AddMcqQuestionCommand(
    int LessonId,
    string Prompt,
    IReadOnlyList<string> Options,
    int CorrectOptionIndex);

public sealed record AddWritingQuestionCommand(
    int LessonId,
    string Prompt,
    IReadOnlyList<string> AcceptedAnswers);

public sealed record LessonQuestionMutationResult(
    bool Succeeded,
    string? Error = null,
    int? QuestionId = null);

/// <summary>Learner-facing practice question — no correct answer.</summary>
public sealed record PracticeQuestionItem(
    int Id,
    string Type,
    string Prompt,
    IReadOnlyList<string> Options);

public sealed record PracticeBundle(
    int LessonId,
    string LessonTitle,
    IReadOnlyList<PracticeQuestionItem> Questions);

public sealed record GradeMcqResult(
    bool Succeeded,
    bool IsCorrect = false,
    int? CorrectOptionIndex = null,
    string? Error = null);

public sealed record GradeWritingResult(
    bool Succeeded,
    bool IsCorrect = false,
    IReadOnlyList<string>? AcceptedAnswers = null,
    string? Error = null);
