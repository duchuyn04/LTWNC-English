namespace ltwnc.Services.Lessons;

public sealed record LessonListItem(
    int Id,
    string Title,
    string? Summary,
    string Status,
    int SortOrder,
    DateTime UpdatedAtUtc);

public sealed record LessonDetail(
    int Id,
    string Title,
    string? Summary,
    string ContentMarkdown,
    string ContentHtml,
    string Status,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

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
