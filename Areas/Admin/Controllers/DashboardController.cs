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

    [HttpGet("/Admin")]
    public async Task<IActionResult> Index(int? days, CancellationToken cancellationToken)
    {
        AdminDashboardSnapshot snapshot = await _kpiService.GetSnapshotAsync(days, cancellationToken);
        return View(AdminDashboardKpiService.ToViewModel(snapshot));
    }
}
