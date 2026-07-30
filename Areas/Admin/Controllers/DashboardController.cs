using ltwnc.Services.AdminDashboard;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

// Hiển thị các việc cần xử lý, trạng thái AI và hoạt động học tập tổng hợp.
[Area("Admin")]
public sealed class DashboardController : Controller
{
    private readonly AdminDashboardService _dashboardService;

    public DashboardController(AdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("/Admin")]
    public async Task<IActionResult> Index(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        return View(await _dashboardService.GetAsync(from, to, cancellationToken));
    }
}
