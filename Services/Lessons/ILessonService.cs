namespace ltwnc.Services.Lessons;

public interface ILessonService
{
    Task<IReadOnlyList<LessonListItem>> ListForAdminAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LessonListItem>> ListPublishedAsync(CancellationToken cancellationToken = default);

    Task<LessonDetail?> GetForAdminAsync(int id, CancellationToken cancellationToken = default);

    Task<LessonDetail?> GetPublishedAsync(int id, CancellationToken cancellationToken = default);

    Task<LessonSaveResult> SaveAsync(LessonSaveCommand command, CancellationToken cancellationToken = default);

    string RenderMarkdown(string markdown);

    Task<IReadOnlyList<LessonQuestionAdminItem>> ListQuestionsForAdminAsync(
        int lessonId,
        CancellationToken cancellationToken = default);

    Task<LessonQuestionMutationResult> AddMcqQuestionAsync(
        AddMcqQuestionCommand command,
        CancellationToken cancellationToken = default);

    Task<LessonQuestionMutationResult> AddWritingQuestionAsync(
        AddWritingQuestionCommand command,
        CancellationToken cancellationToken = default);

    Task<LessonQuestionMutationResult> DeleteQuestionAsync(
        int lessonId,
        int questionId,
        CancellationToken cancellationToken = default);

    /// <summary>Published lesson with ≥1 question only; answers not exposed.</summary>
    Task<PracticeBundle?> GetPracticeBundleAsync(
        int lessonId,
        CancellationToken cancellationToken = default);

    Task<GradeMcqResult> GradeMcqAsync(
        int lessonId,
        int questionId,
        int selectedIndex,
        bool publishedOnly = true,
        CancellationToken cancellationToken = default);

    Task<GradeWritingResult> GradeWritingAsync(
        int lessonId,
        int questionId,
        string answer,
        bool publishedOnly = true,
        CancellationToken cancellationToken = default);
}
