using ltwnc.Models.ViewModels.Leaderboard;

namespace ltwnc.Services.Leaderboard;

public interface ILeaderboardService
{
    // Lấy dữ liệu bảng xếp hạng theo số ngày được chọn.
    // Đồng thời xác định vị trí của người đang xem nếu họ đã đăng nhập
    Task<LeaderboardPageViewModel> GetPageAsync(
        // Khoảng thời gian dùng để tính xếp hạng, ví dụ 7 hoặc 30 ngày
        int periodDays,
        // ID người đang xem; có thể null nếu người dùng chưa đăng nhập
        string? viewerUserId,
        // Cho phép hủy quá trình lấy dữ liệu khi request bị ngắt
        CancellationToken cancellationToken = default);
}
