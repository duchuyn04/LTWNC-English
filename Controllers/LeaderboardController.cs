using ltwnc.Services.Auth;
using ltwnc.Services.Leaderboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Cho phép cả người đã đăng nhập và chưa đăng nhập
// truy cập các action trong controller này.
[AllowAnonymous]

//Controller tiếp nhận request liên quan đến trang bảng xếp hạng
public sealed class LeaderboardController : Controller
{
    //Service chịu trách nhiệm xử lý và lấy dữ liệu bảng xếp hạng
    private readonly ILeaderboardService _leaderboardService;

    //Service cung cấp thông tin về người dùng hiện tại
    private readonly ICurrentUser _currentUser;

    // ASP.NET Core Dependency Injection truyền hai dependency vào khi tạo LeaderboardController.
    public LeaderboardController(
        ILeaderboardService leaderboardService,
        ICurrentUser currentUser)
    {
        // Lưu service để action có thể sử dụng
        _leaderboardService = leaderboardService;
        // Lưu thông tin người dùng hiện tại
        _currentUser = currentUser;
    }

    // Xử lý HTTP GET tại đường dẫn /Leaderboard
    [HttpGet("/Leaderboard")]
    public async Task<IActionResult> Index(
        // Khoảng thời gian bảng xếp hạng;
        // nếu không truyền thì mặc định là 7
        int period = 7,
        // Cho phép hủy công việc khi request bị hủy
        CancellationToken cancellationToken = default)
    {
        // Yêu cầu service tạo dữ liệu trang bảng xếp hạng
        // dựa trên khoảng thời gian và người đang xem
        var model = await _leaderboardService.GetPageAsync(
            period,
            _currentUser.UserId,
            cancellationToken);
        return View(model);
    }
}
