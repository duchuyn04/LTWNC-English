using ltwnc.Services.Achievements;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Trang huy hiệu user đang login. Khách bị Challenge sang đăng nhập.
[Authorize]
public class AchievementsController : Controller
{
    // Rescan unlock + map list view model
    private readonly IAchievementService _achievementService;

    // User hiện tại từ cookie claims
    private readonly ICurrentUser _currentUser;

    public AchievementsController(
        IAchievementService achievementService,
        ICurrentUser currentUser)
    {
        _achievementService = achievementService;
        _currentUser = currentUser;
    }

    // GET /Achievements: rescan, banner TempData nếu vừa mở huy hiệu, render list
    [Route("/Achievements")]
    public async Task<IActionResult> Index()
    {
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        AchievementPageModel page = await _achievementService.GetPageAsync(userId);

        // Banner một lần khi rescan vừa mở huy hiệu mới
        if (page.NewlyUnlockedTitles.Count > 0)
        {
            TempData["AchievementUnlock"] =
                "Bạn vừa mở: " + string.Join(", ", page.NewlyUnlockedTitles);
        }

        return View(page.Items);
    }
}
