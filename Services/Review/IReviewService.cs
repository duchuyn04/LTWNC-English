using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;

namespace ltwnc.Services.Review;

public interface IReviewService
{
    Task<ReviewSessionViewModel?> GetActiveSessionAsync(string userId);

    /// <summary>
    /// Legacy: bắt đầu ôn trên mọi bộ thẻ của user.
    /// Controller/UI hiện chỉ dùng <see cref="StartAsync(string, int)"/> (per-set).
    /// Giữ overload này cho test và tương thích ngược — không wire route mới.
    /// </summary>
    Task<ReviewSessionViewModel?> StartAsync(string userId);

    Task<ReviewSetViewModel?> GetSetAsync(string userId, int setId);

    Task<ReviewSessionViewModel?> StartAsync(string userId, int setId);

    Task<ReviewSessionViewModel?> GetSessionAsync(int sessionId, string userId);

    Task<ReviewRatingResult> RateAsync(
        string userId,
        int sessionId,
        int flashcardId,
        ReviewRating rating,
        bool answerRevealed);

    Task<ReviewSessionViewModel?> EndAsync(string userId, int sessionId);
}
