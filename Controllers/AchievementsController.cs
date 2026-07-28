using ltwnc.Services.Achievements;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// Hiển thị thành tích của người đang đăng nhập; khách sẽ được yêu cầu đăng nhập.
[Authorize]
public class AchievementsController : Controller
{
    // Service kiểm tra thành tích mới và tạo dữ liệu cho trang danh sách.
    private readonly IAchievementService _achievementService;

    // Thông tin người dùng hiện tại được đọc từ cookie đăng nhập.
    private readonly ICurrentUser _currentUser;

    // Nhận service thành tích và thông tin người dùng qua dependency injection.
    public AchievementsController(
        IAchievementService achievementService,
        ICurrentUser currentUser)
    {
        // 1. Lưu các service để action Index sử dụng.
        _achievementService = achievementService;
        _currentUser = currentUser;
    }

    // Kiểm tra thành tích mới, lưu thông báo một lần và hiển thị danh sách huy hiệu.
    [Route("/Achievements")]
    public async Task<IActionResult> Index()
    {
        // 1. Lấy mã người dùng hiện tại và yêu cầu đăng nhập nếu chưa có.
        string? userId = _currentUser.UserId;
        if (userId == null)
        {
            return Challenge();
        }

        // 2. Kiểm tra thành tích mới và lấy dữ liệu trang.
        AchievementPageModel page = await _achievementService.GetPageAsync(userId);

        // 3. TempData giúp thông báo thành tích mới chỉ xuất hiện một lần.
        if (page.NewlyUnlockedTitles.Count > 0)
        {
            TempData["AchievementUnlock"] =
                "Bạn vừa mở: " + string.Join(", ", page.NewlyUnlockedTitles);
        }

        // 4. Hiển thị toàn bộ thành tích trong view.
        return View(page.Items);
    }
}
