using Microsoft.AspNetCore.Mvc;
using ltwnc.Services;
using ltwnc.Models.ViewModels.Account;

namespace ltwnc.Controllers;

// Controller xử lý đăng ký, đăng nhập, đăng xuất
public class AccountController : Controller
{
    private readonly IAccountService _accountService;

    // Inject service xử lý nghiệp vụ tài khoản
    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
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

        // Gọi service tạo tài khoản mới
        var result = await _accountService.RegisterAsync(model.Email, model.Username, model.Password);
        if (result.Succeeded)
        {
            // Đăng ký thành công → tự động đăng nhập
            await _accountService.LoginAsync(model.Email, model.Password, false);
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

        // Gọi service xác thực đăng nhập
        var result = await _accountService.LoginAsync(model.Email, model.Password, model.RememberMe);
        if (result.Succeeded)
        {
            // Đăng nhập thành công → chuyển về trang chủ
            return RedirectToAction("Index", "Home");
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
        await _accountService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }
}
