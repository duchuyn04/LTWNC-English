using ltwnc.Models.ViewModels.Leaderboard;

namespace ltwnc.Services.Leaderboard;

public interface ILeaderboardService
{
    Task<LeaderboardPageViewModel> GetPageAsync(
        int periodDays,
        string? viewerUserId,
        CancellationToken cancellationToken = default);
}
