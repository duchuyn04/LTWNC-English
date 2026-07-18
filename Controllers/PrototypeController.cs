using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Controllers;

// PROTOTYPE - throwaway UI for comparing leaderboard layouts.
[AllowAnonymous]
public class PrototypeController : Controller
{
    private readonly IHostEnvironment _environment;

    public PrototypeController(IHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("/Prototype/Leaderboard")]
    public IActionResult Leaderboard(string variant = "a", int period = 7, string state = "outside")
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        ViewData["Variant"] = variant is "a" or "b" or "c" ? variant : "a";
        ViewData["Period"] = period == 30 ? 30 : 7;
        ViewData["State"] = state is "normal" or "outside" or "empty" ? state : "outside";
        return View();
    }
}
