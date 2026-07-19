using ltwnc.Models.Entities;

namespace ltwnc.Services.ContentModeration;

// Hợp đồng nghiệp vụ kiểm duyệt bộ flashcard cho khu vực Admin.
public interface IContentModerationService
{
    Task<AdminContentSetPage> SearchSetsAsync(
        AdminContentSetQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminContentSetDetailsResult> GetDetailsAsync(
        int flashcardSetId,
        AdminContentSetAccessCommand access,
        CancellationToken cancellationToken = default);

    Task<ContentModerationOperationResult> QuarantineSetAsync(
        QuarantineFlashcardSetCommand command,
        CancellationToken cancellationToken = default);

    Task<ContentModerationOperationResult> QuarantineFromReportAsync(
        QuarantineFromReportCommand command,
        CancellationToken cancellationToken = default);

    Task<ContentModerationOperationResult> RestoreSetAsync(
        RestoreFlashcardSetCommand command,
        CancellationToken cancellationToken = default);
}

