using ltwnc.Models.ViewModels.Review;

namespace ltwnc.Services.Review;

public interface IReviewSettingsService
{
    Task<ReviewSettingsViewModel?> GetAsync(
        string userId,
        int flashcardSetId,
        CancellationToken cancellationToken = default);

    Task<ReviewSettingsViewModel?> GetOrCreateAsync(
        string userId,
        int flashcardSetId,
        CancellationToken cancellationToken = default);

    Task<ReviewSettingsViewModel?> SaveAsync(
        string userId,
        int flashcardSetId,
        ReviewSettingsViewModel input,
        CancellationToken cancellationToken = default);
}
