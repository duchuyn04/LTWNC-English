using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.AdminAchievements;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// Màn hình quản trị thành tích: chỉ đọc danh mục/kết quả và cho phép đồng bộ lại từ dữ liệu học tập.
[Area("Admin")]
[Route("Admin/Achievements")]
public sealed class AchievementsController : Controller
{
    private readonly IAdminAchievementService _achievementService;

    // Nhận service nghiệp vụ để controller chỉ điều phối request/response.
    public AchievementsController(IAdminAchievementService achievementService)
    {
        _achievementService = achievementService;
    }

    // Hiển thị danh mục thành tích từ source code và kết quả theo người dùng.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        int page = AdminAchievementService.DefaultPage,
        int pageSize = AdminAchievementService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new AdminAchievementQuery(search, page, pageSize);
        AdminAchievementOverview overview =
            await _achievementService.GetOverviewAsync(query, cancellationToken);
        return View(AdminAchievementViewModelMapper.ToIndexViewModel(overview, query));
    }

    // Xử lý đồng bộ lại cho một người dùng; form bắt buộc có antiforgery, lý do và checkbox xác nhận.
    [HttpPost("ResyncUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResyncUser(
        AdminAchievementResyncUserInputModel input,
        CancellationToken cancellationToken = default)
    {
        AdminAchievementSyncResult result = await _achievementService.ResyncUserAsync(
            BuildUserCommand(input),
            cancellationToken);
        StoreMessage(result.Succeeded, result.Message);

        return RedirectToAction(nameof(Index), new { search = input.TargetUserId });
    }

    // Xử lý đồng bộ lại toàn hệ thống theo lô; không có chức năng sửa/cấp/thu hồi thủ công.
    [HttpPost("ResyncAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResyncAll(
        AdminAchievementResyncAllInputModel input,
        CancellationToken cancellationToken = default)
    {
        AdminAchievementBatchSyncResult result = await _achievementService.ResyncAllAsync(
            BuildBatchCommand(input),
            cancellationToken);
        StoreMessage(result.Succeeded, result.Message);

        return RedirectToAction(nameof(Index));
    }

    // Dùng ngữ cảnh Admin hiện tại để tạo lệnh đồng bộ cho một người dùng.
    private AdminAchievementSyncCommand BuildUserCommand(
        AdminAchievementResyncUserInputModel input)
    {
        string actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new AdminAchievementSyncCommand(
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            TargetUserId: input.TargetUserId,
            Reason: input.Reason,
            Confirmed: input.Confirmed,
            CorrelationId: HttpContext.TraceIdentifier);
    }

    // Dùng ngữ cảnh Admin hiện tại để tạo lệnh đồng bộ toàn hệ thống.
    private AdminAchievementBatchSyncCommand BuildBatchCommand(
        AdminAchievementResyncAllInputModel input)
    {
        string actorUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new AdminAchievementBatchSyncCommand(
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            Reason: input.Reason,
            Confirmed: input.Confirmed,
            BatchSize: input.BatchSize,
            CorrelationId: HttpContext.TraceIdentifier);
    }

    // Lưu thông báo qua TempData để hiển thị sau redirect.
    private void StoreMessage(bool succeeded, string message)
    {
        if (succeeded)
        {
            TempData["AdminAchievementsSuccess"] = message;
            return;
        }

        TempData["AdminAchievementsError"] = message;
    }
}
