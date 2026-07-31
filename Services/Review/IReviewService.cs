using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;

namespace ltwnc.Services.Review;

public interface IReviewService
{
    Task<ReviewSessionViewModel?> GetActiveSessionAsync(string userId);

    Task<ReviewSessionViewModel?> StartAsync(string userId);

    Task<ReviewSessionViewModel?> GetSessionAsync(int sessionId, string userId);

    Task<ReviewRatingResult> RateAsync(
        string userId,
        int sessionId,
        int flashcardId,
        ReviewRating rating,
        bool answerRevealed);
}
