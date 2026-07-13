using ltwnc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Trang huy hiệu user đang login. Khách bị Challenge sang đăng nhập.
[Authorize]
public class AchievementsController : Controller
{
    // Rescan unlock + map list view model
    private readonly AchievementService _achievementService;

    // User hiện tại từ cookie
    private readonly UserManager<IdentityUser> _userManager;

    // Inject service thành tích và UserManager
    public AchievementsController(
        AchievementService achievementService,
        UserManager<IdentityUser> userManager)
    {
        _achievementService = achievementService;
        _userManager = userManager;
    }

    // GET /Achievements: rescan, banner TempData nếu vừa mở huy hiệu, render list
    [Route("/Achievements")]
    public async Task<IActionResult> Index()
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        AchievementPageModel page = await _achievementService.GetPageAsync(user.Id);

        // Banner một lần khi rescan vừa mở huy hiệu mới
        if (page.NewlyUnlockedTitles.Count > 0)
        {
            TempData["AchievementUnlock"] =
                "Bạn vừa mở: " + string.Join(", ", page.NewlyUnlockedTitles);
        }

        return View(page.Items);
    }
}
