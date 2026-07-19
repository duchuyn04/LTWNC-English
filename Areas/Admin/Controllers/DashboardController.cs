using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
public sealed class DashboardController : Controller
{
    [HttpGet("/Admin")]
    public IActionResult Index()
    {
        return View();
    }
}
