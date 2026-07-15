using Microsoft.AspNetCore.Mvc;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Auth;

namespace ltwnc.Controllers;

// Đăng ký, đăng nhập, đăng xuất (cookie auth + IAuthService).
public class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
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

        AuthResult result = await _authService.RegisterAsync(
            model.Username,
            model.Email,
            model.Password);

        if (result.Succeeded)
        {
            return RedirectToAction("Index", "Home");
        }

        foreach (string error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
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

        AuthResult result = await _authService.LoginAsync(
            model.Email,
            model.Password,
            model.RememberMe);

        if (result.Succeeded)
        {
            return Redirect("/Set");
        }

        foreach (string error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }
}
