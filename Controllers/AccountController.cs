using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Controllers;

public class AccountController : Controller
{
    private static readonly TimeSpan RegisterCookieLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan RememberMeCookieLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(1);

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IAdminAuditService _adminAuditService;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        AppDbContext db,
        TimeProvider timeProvider,
        IAdminAuditService adminAuditService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _timeProvider = timeProvider;
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

        var user = new IdentityUser
        {
            UserName = model.Username.Trim(),
            Email = model.Email.Trim()
        };

        IdentityResult result;

        try
        {
            result = await _userManager.CreateAsync(user, model.Password);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmailViolation(ex))
        {
            ModelState.AddModelError(string.Empty, "Email đã được sử dụng.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, MapIdentityError(error));
            }

            return View(model);
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            _db.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            });
            await _db.SaveChangesAsync();
        }
        catch
        {
            await _userManager.DeleteAsync(user);
            throw;
        }

        await _signInManager.SignInAsync(
            user,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(RegisterCookieLifetime)
            });

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

        IdentityUser? user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        bool isAdmin = await _userManager.IsInRoleAsync(
            user,
            AdminRoleBootstrapper.AdminRole);
        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: true);

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
        await _signInManager.SignInAsync(
            user,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime)
            });

        if (isAdmin)
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
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private static string MapIdentityError(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.DuplicateEmail) or "DuplicateEmail" => "Email đã được sử dụng.",
        nameof(IdentityErrorDescriber.DuplicateUserName) or "DuplicateUserName" => "Tên đăng nhập đã được sử dụng.",
        nameof(IdentityErrorDescriber.InvalidUserName) or "InvalidUserName" => "Tên đăng nhập không hợp lệ.",
        nameof(IdentityErrorDescriber.PasswordTooShort) or "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) or "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
        nameof(IdentityErrorDescriber.PasswordRequiresLower) or "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) or "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
        _ => "Đăng ký không thành công. Vui lòng kiểm tra lại thông tin."
    };

    private string GetAuthenticatedLandingPath() =>
        User.IsInRole(AdminRoleBootstrapper.AdminRole) ? "/Admin" : "/Set";

    // Ghi audit sau khi Admin đăng nhập thành công; không ghi mật khẩu hoặc thông tin nhạy cảm.
    private async Task RecordAdminSignInAuditAsync(IdentityUser user)
    {
        await _adminAuditService.RecordAsync(new AdminAuditEntry(
            ActorUserId: user.Id,
            ActorDisplay: user.Email ?? user.UserName ?? user.Id,
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "IdentityUser",
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

    private static bool IsDuplicateEmailViolation(DbUpdateException exception)
    {
        string message = exception.InnerException?.Message ?? exception.Message;

        return message.Contains("AspNetUsers", StringComparison.OrdinalIgnoreCase)
            && message.Contains("Email", StringComparison.OrdinalIgnoreCase)
            && (
                message.Contains("unique constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("EmailIndex", StringComparison.OrdinalIgnoreCase));
    }
}
