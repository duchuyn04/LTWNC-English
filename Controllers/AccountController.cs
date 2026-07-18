using ltwnc.Models.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Controllers;

public class AccountController : Controller
{
    private static readonly TimeSpan RegisterCookieLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan RememberMeCookieLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(1);

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
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
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
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

        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
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
