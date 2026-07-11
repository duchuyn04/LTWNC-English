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

    // GET /Achievements — danh sách huy hiệu đã mở / chưa mở
    [Route("/Achievements")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Lấy danh mục + trạng thái mở khóa của đúng user này
        var model = await _achievementService.GetCatalogWithStatusAsync(user.Id);
        return View(model);
    }
}
