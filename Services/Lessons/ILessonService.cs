namespace ltwnc.Services.Lessons;

public interface ILessonService
{
    Task<IReadOnlyList<LessonListItem>> ListForAdminAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LessonListItem>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<LessonDetail?> GetForAdminAsync(int id, CancellationToken cancellationToken = default);

    Task<LessonDetail?> GetPublishedAsync(int id, CancellationToken cancellationToken = default);

    Task<LessonSaveResult> SaveAsync(LessonSaveCommand command, CancellationToken cancellationToken = default);

    string RenderMarkdown(string markdown);
}
