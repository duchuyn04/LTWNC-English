using ltwnc.Models.ViewModels.Leaderboard;

namespace ltwnc.Services.Leaderboard;

// Hợp đồng tạo dữ liệu bảng xếp hạng theo khoảng thời gian.
public interface ILeaderboardService
{
    // Lấy bảng xếp hạng và vị trí của người đang xem nếu có.
    Task<LeaderboardPageViewModel> GetPageAsync(
        int periodDays,
        string? viewerUserId,
        CancellationToken cancellationToken = default);
}
