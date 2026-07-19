using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.AdminAchievements;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// Man hinh quan tri thanh tich: chi doc catalog/ket qua va cho phep dong bo lai tu du lieu hoc tap.
[Area("Admin")]
[Route("Admin/Achievements")]
public sealed class AchievementsController : Controller
{
    private readonly IAdminAchievementService _achievementService;

    // Nhan service nghiep vu de controller chi dieu phoi request/response.
    public AchievementsController(IAdminAchievementService achievementService)
    {
        _achievementService = achievementService;
    }

    // Hien thi catalog thanh tich tu source code va ket qua theo nguoi dung.
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

    // Xu ly dong bo lai cho mot nguoi dung; form bat buoc co antiforgery, ly do va checkbox xac nhan.
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

    // Xu ly dong bo lai toan he thong theo lo; khong co chuc nang sua/cap/thu hoi thu cong.
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

    // Dung ngu canh Admin hien tai de tao lenh sync mot user.
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

    // Dung ngu canh Admin hien tai de tao lenh sync toan he thong.
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

    // Luu thong bao qua TempData de hien sau redirect.
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
