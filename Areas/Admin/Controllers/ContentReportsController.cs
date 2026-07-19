using System.Security.Claims;
using ltwnc.Areas.Admin.Models;
using ltwnc.Services.ContentReports;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/ContentReports")]
public sealed class ContentReportsController : Controller
{
    private readonly IContentReportService _reportService;

    // Nhận service xử lý nghiệp vụ để controller chỉ điều phối request/response.
    public ContentReportsController(IContentReportService reportService)
    {
        _reportService = reportService;
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
}
