using ltwnc.Services.Auth;
using ltwnc.Services.Leaderboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

[AllowAnonymous]
public sealed class LeaderboardController : Controller
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly ICurrentUser _currentUser;

    public LeaderboardController(
        ILeaderboardService leaderboardService,
        ICurrentUser currentUser)
    {
        _leaderboardService = leaderboardService;
        _currentUser = currentUser;
    }

    [HttpGet("/Leaderboard")]
    public async Task<IActionResult> Index(
        int period = 7,
        CancellationToken cancellationToken = default)
    {
        var model = await _leaderboardService.GetPageAsync(
            period,
            _currentUser.UserId,
            cancellationToken);
        return View(model);
    }
}
