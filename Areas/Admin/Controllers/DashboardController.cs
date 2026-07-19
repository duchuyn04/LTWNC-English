using Microsoft.AspNetCore.Mvc;
using ltwnc.Services.AdminDashboard;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class DashboardController : Controller
{
    private readonly IAdminDashboardKpiService _kpiService;

    public DashboardController(IAdminDashboardKpiService kpiService)
    {
        _kpiService = kpiService;
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
}
