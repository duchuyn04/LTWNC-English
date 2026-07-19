using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Areas.Admin.Controllers;

[Area("Admin")]
[Route("Admin/Security")]
[Authorize(Policy = AdminAreaPolicy.RecentAuthenticationName)]
public sealed class SecurityController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;

    public SecurityController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        IdentityUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        int recoveryCodeCount = await _userManager.CountRecoveryCodesAsync(user);
        return View(recoveryCodeCount);
    }

}
