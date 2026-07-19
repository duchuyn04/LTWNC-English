using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.ContentModeration;
using ltwnc.Services.ContentReports;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/ContentReports")]
public sealed class ContentReportsController : Controller
{
    private readonly IContentReportService _reportService;
    private readonly IContentModerationService _moderationService;

    // Nhận service xử lý nghiệp vụ để controller chỉ điều phối request/response.
    public ContentReportsController(
        IContentReportService reportService,
        IContentModerationService moderationService)
    {
        _reportService = reportService;
        _moderationService = moderationService;
    }

    // Hiển thị hàng đợi báo cáo với lọc, sắp xếp và phân trang phía máy chủ.
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? reason,
        string? sort,
        int page = ContentReportService.DefaultPage,
        int pageSize = ContentReportService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        AdminContentReportPage result = await _reportService.SearchForAdminAsync(
            new AdminContentReportQuery(search, status, reason, sort, page, pageSize),
            cancellationToken);
        int overduePendingCount = await _reportService.CountPendingOlderThanAsync(
            TimeSpan.FromHours(24),
            cancellationToken);

        AdminContentReportIndexViewModel model =
            AdminContentReportViewModelMapper.ToIndexViewModel(
                result,
                _reportService.GetReasonOptions(),
                search,
                status,
                reason,
                sort,
                overduePendingCount);

        return View(model);
    }

    // POST bác bỏ báo cáo, bắt buộc antiforgery, confirm phía view và lý do xử lý.
    [HttpPost("{id:long}/Dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(
        long id,
        AdminContentReportDismissInputModel input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["ContentReportsError"] = "Vui lòng nhập lý do xử lý trước khi bác bỏ báo cáo.";
            return RedirectToAction(nameof(Index));
        }

        ContentReportOperationResult result = await _reportService.DismissAsync(
            BuildDismissCommand(id, input),
            cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Index));
    }

    // POST cách ly từ một báo cáo đang chờ, xử lý báo cáo và bộ flashcard trong cùng transaction.
    [HttpPost("{id:long}/Quarantine")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quarantine(
        long id,
        AdminContentReportQuarantineInputModel input,
        CancellationToken cancellationToken = default)
    {
        ContentModerationOperationResult result =
            await _moderationService.QuarantineFromReportAsync(
                BuildQuarantineFromReportCommand(id, input),
                cancellationToken);
        StoreOperationMessage(result);

        return RedirectToAction(nameof(Index));
    }

    // Dựng lệnh nghiệp vụ từ Admin hiện tại, trace id và dữ liệu form.
    private DismissContentReportCommand BuildDismissCommand(
        long reportId,
        AdminContentReportDismissInputModel input)
    {
        string actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new DismissContentReportCommand(
            ReportId: reportId,
            Version: input.Version,
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            Reason: input.Reason,
            CorrelationId: HttpContext.TraceIdentifier);
    }

    // Dựng lệnh cách ly từ báo cáo, gồm cả version report và version bộ để bắt xung đột.
    private QuarantineFromReportCommand BuildQuarantineFromReportCommand(
        long reportId,
        AdminContentReportQuarantineInputModel input)
    {
        string actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        string actorDisplay = User.Identity?.Name ?? actorUserId;

        return new QuarantineFromReportCommand(
            ReportId: reportId,
            ReportVersion: input.ReportVersion,
            FlashcardSetVersion: input.FlashcardSetVersion,
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            PublicReason: input.PublicReason,
            InternalNote: input.InternalNote,
            Evidence: input.Evidence,
            Confirmed: input.Confirmed,
            CorrelationId: HttpContext.TraceIdentifier);
    }

    // Lưu thông báo qua TempData để hiện sau redirect POST-Redirect-GET.
    private void StoreOperationMessage(ContentReportOperationResult result)
    {
        if (result.Succeeded)
        {
            TempData["ContentReportsSuccess"] = result.Message;
            return;
        }

        TempData["ContentReportsError"] = result.Message;
    }

    // Lưu thông báo cho thao tác cách ly đi qua service kiểm duyệt nội dung.
    private void StoreOperationMessage(ContentModerationOperationResult result)
    {
        if (result.Succeeded)
        {
            TempData["ContentReportsSuccess"] = result.Message;
            return;
        }

        TempData["ContentReportsError"] = result.Message;
    }
}
