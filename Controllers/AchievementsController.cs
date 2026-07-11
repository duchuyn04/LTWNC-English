using ltwnc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// ============================================================
// Trang xem thành tích (huy hiệu) của user đang đăng nhập.
// Chỉ người đã login mới vào được — khách sẽ bị chuyển sang đăng nhập.
// ============================================================
[Authorize]
public class AchievementsController : Controller
{
    private readonly AchievementService _achievementService;
    private readonly UserManager<IdentityUser> _userManager;

    public AchievementsController(
        AchievementService achievementService,
        UserManager<IdentityUser> userManager)
    {
        _achievementService = achievementService;
        _userManager = userManager;
    }

    // GET /Achievements — rescan + danh sách huy hiệu đã mở / chưa mở
    [Route("/Achievements")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Rescan mở khóa thiếu + lấy progress/CTA cho từng huy hiệu
        var page = await _achievementService.GetPageAsync(user.Id);

        // Banner một lần (TempData) khi rescan vừa mở huy hiệu mới
        if (page.NewlyUnlockedTitles.Count > 0)
        {
            TempData["AchievementUnlock"] =
                "Bạn vừa mở: " + string.Join(", ", page.NewlyUnlockedTitles);
        }

        return View(page.Items);
    }
}
