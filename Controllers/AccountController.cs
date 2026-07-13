using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using ltwnc.Models.ViewModels.Account;

namespace ltwnc.Controllers;

// Đăng ký, đăng nhập, đăng xuất (ASP.NET Identity).
public class AccountController : Controller
{
    // Tạo user, tìm email
    private readonly UserManager<IdentityUser> _userManager;

    // Cookie sign-in / sign-out
    private readonly SignInManager<IdentityUser> _signInManager;

    // Inject Identity managers
    public AccountController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // GET form đăng ký
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // POST đăng ký: CreateAsync rồi SignIn 1 ngày; lỗi Identity vào ModelState
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        IdentityUser user = new IdentityUser
        {
            UserName = model.Username,
            Email = model.Email
        };

        IdentityResult result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
            });
            return RedirectToAction("Index", "Home");
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET form đăng nhập
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // POST đăng nhập theo email + password; RememberMe 30 ngày, không thì 1 ngày
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        IdentityUser? user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null)
        {
            // Identity.SignInResult (không nhầm Mvc.SignInResult)
            Microsoft.AspNetCore.Identity.SignInResult result =
                await _signInManager.CheckPasswordSignInAsync(
                    user,
                    model.Password,
                    lockoutOnFailure: false);

            if (result.Succeeded)
            {
                TimeSpan cookieLifetime = model.RememberMe
                    ? TimeSpan.FromDays(30)
                    : TimeSpan.FromDays(1);

                await _signInManager.SignInAsync(user, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(cookieLifetime)
                });

                return Redirect("/Set");
            }
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    // POST đăng xuất, về Home
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
