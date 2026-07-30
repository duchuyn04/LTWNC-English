namespace ltwnc.Services.ContentModeration;

// Hợp đồng nghiệp vụ kiểm duyệt bộ flashcard cho khu vực Admin.
public interface IContentModerationService
{
    Task<ContentModerationOperationResult> QuarantineFromReportAsync(
        QuarantineFromReportCommand command,
        CancellationToken cancellationToken = default);

    Task<ContentModerationOperationResult> RestoreSetAsync(
        RestoreFlashcardSetCommand command,
        CancellationToken cancellationToken = default);
}
