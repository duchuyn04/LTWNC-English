using ltwnc.Services.AdminDashboard;
using ltwnc.Services.AdminExports;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class DashboardController : Controller
{
    private readonly IAdminDashboardKpiService _kpiService;
    private readonly IAdminExportService _exportService;
    private readonly ICurrentUser _currentUser;

    // Nhận service KPI, export và current user để các endpoint dashboard dùng chung actor audit.
    public DashboardController(
        IAdminDashboardKpiService kpiService,
        IAdminExportService exportService,
        ICurrentUser currentUser)
    {
        _kpiService = kpiService;
        _exportService = exportService;
        _currentUser = currentUser;
    }

    // Render trang dashboard ban đầu bằng dữ liệu server-side để không phụ thuộc JavaScript.
    [HttpGet("/Admin")]
    public async Task<IActionResult> Index(int? days, CancellationToken cancellationToken)
    {
        AdminDashboardSnapshot snapshot = await _kpiService.GetSnapshotAsync(days, cancellationToken);
        return View(AdminDashboardKpiService.ToViewModel(snapshot));
    }

    // Endpoint AJAX chỉ đọc cho snapshot vận hành; luôn tắt cache để tránh dùng dữ liệu cũ.
    [HttpGet("/Admin/Snapshot")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Snapshot(int? days, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        AdminDashboardLiveSnapshot snapshot =
            await _kpiService.GetLiveSnapshotAsync(days, cancellationToken);
        return Json(snapshot);
    }

    // Xuất CSV KPI theo cùng bộ lọc ngày của dashboard và không trả dữ liệu cá nhân thô.
    [HttpGet("/Admin/Export/Kpis")]
    public async Task<IActionResult> ExportKpis(int? days, CancellationToken cancellationToken)
    {
        AdminCsvExport export = await _exportService.ExportKpisAsync(
            days,
            AdminExportActorFactory.FromCurrentUser(_currentUser),
            cancellationToken);

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        return File(export.Content, "text/csv; charset=utf-8", export.FileName);
    }
}
