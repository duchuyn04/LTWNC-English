using ltwnc.Services.Auth;
using ltwnc.Services.Leaderboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Hiển thị bảng xếp hạng công khai theo khoảng thời gian người dùng chọn.
[AllowAnonymous]
public sealed class LeaderboardController : Controller
{
    // Service tạo bảng xếp hạng và thông tin người dùng hiện tại để đánh dấu vị trí cá nhân.
    private readonly ILeaderboardService _leaderboardService;
    private readonly ICurrentUser _currentUser;

    // Nhận các service cần dùng qua dependency injection.
    public LeaderboardController(
        ILeaderboardService leaderboardService,
        ICurrentUser currentUser)
    {
        // 1. Lưu service bảng xếp hạng và thông tin người dùng hiện tại.
        _leaderboardService = leaderboardService;
        _currentUser = currentUser;
    }

    // Lấy dữ liệu theo số ngày và hiển thị bảng xếp hạng.
    [HttpGet("/Leaderboard")]
    public async Task<IActionResult> Index(
        int period = 7,
        CancellationToken cancellationToken = default)
    {
        // 1. Nhận khoảng thời gian cần xếp hạng, mặc định là 7 ngày.
        // 2. Lấy bảng xếp hạng và vị trí của người dùng hiện tại nếu có.
        // 3. Hiển thị dữ liệu trong view.
        var model = await _leaderboardService.GetPageAsync(
            period,
            _currentUser.UserId,
            cancellationToken);
        return View(model);
    }
}
