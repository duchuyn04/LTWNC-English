using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ltwnc.Controllers;

public class AccountController : Controller
{
    private static readonly TimeSpan RegisterCookieLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan RememberMeCookieLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(1);

    private readonly IAuthService _authService;
    private readonly IAdminAuditService _adminAuditService;

    public AccountController(
        IAuthService authService,
        IAdminAuditService adminAuditService)
    {
        _authService = authService;
        _adminAuditService = adminAuditService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? usernameError = UsernamePolicy.GetValidationError(model.Username);
        if (usernameError != null)
        {
            ModelState.AddModelError(nameof(RegisterViewModel.Username), usernameError);
            return View(model);
        }

        AuthResult result = await _authService.RegisterAsync(
            model.Email.Trim(),
            model.Username.Trim(),
            model.Password);
        if (!result.Succeeded)
        {
            foreach (AuthError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            return View(model);
        }

        AppUser? user = await _authService.FindByEmailAsync(model.Email.Trim());
        if (user != null)
        {
            await _authService.SignInAsync(user, RegisterCookieLifetime);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public Task<IActionResult> Login()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult<IActionResult>(Redirect(GetAuthenticatedLandingPath()));
        }

        return Task.FromResult<IActionResult>(View());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AppUser? user = await _authService.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        AuthResult result = await _authService.ValidateLoginAsync(user, model.Password);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                AddLockedAccountMessage();
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        TimeSpan lifetime = model.RememberMe ? RememberMeCookieLifetime : SessionCookieLifetime;
        await _authService.SignInAsync(user, lifetime);

        if (user.IsAdmin)
        {
            await RecordAdminSignInAuditAsync(user);
            return Redirect("/Admin");
        }

        return Redirect("/Set");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private string GetAuthenticatedLandingPath() =>
        User.HasClaim(AppClaimTypes.IsAdmin, "true") ? "/Admin" : "/Set";

    // Ghi audit sau khi Admin đăng nhập thành công; không ghi mật khẩu hoặc thông tin nhạy cảm.
    private async Task RecordAdminSignInAuditAsync(AppUser user)
    {
        await _adminAuditService.RecordAsync(new AdminAuditEntry(
            ActorUserId: user.Id,
            ActorDisplay: user.Email,
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "AppUser",
            TargetId: user.Id,
            CorrelationId: HttpContext.TraceIdentifier));
    }

    // Thông báo chung cho tài khoản bị khóa, không lộ lý do nội bộ do Admin nhập.
    private void AddLockedAccountMessage()
    {
        ModelState.AddModelError(
            string.Empty,
            "Tài khoản hiện không thể đăng nhập. Vui lòng liên hệ bộ phận hỗ trợ để được kiểm tra.");
    }
}
