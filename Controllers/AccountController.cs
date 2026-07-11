using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Account;

namespace ltwnc.Controllers;

// Controller xử lý đăng ký, đăng nhập, đăng xuất
public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // Hiển thị form đăng ký
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // Xử lý dữ liệu đăng ký từ form
    [HttpPost]
    [ValidateAntiForgeryToken] // Bảo vệ chống CSRF
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // Kiểm tra dữ liệu đầu vào hợp lệ
        if (!ModelState.IsValid) return View(model);

        // Tạo tài khoản mới bằng UserManager
        var user = new IdentityUser { UserName = model.Username, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // Đăng ký thành công → tự động đăng nhập (giữ 1 ngày)
            await _signInManager.SignInAsync(user, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
            });
            return RedirectToAction("Index", "Home");
        }

        // Nếu có lỗi (email trùng, mật khẩu yếu...) → hiển thị lỗi
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return View(model);
    }

    // Hiển thị form đăng nhập
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // Xử lý dữ liệu đăng nhập từ form
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Kiểm tra dữ liệu đầu vào hợp lệ
        if (!ModelState.IsValid) return View(model);

        // Tìm user theo email và đăng nhập bằng SignInManager
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null)
        {
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(model.RememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromDays(1))
                });
                return Redirect("/Set");
            }
        }

        // Đăng nhập thất bại → hiển thị lỗi
        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    // Xử lý đăng xuất
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
