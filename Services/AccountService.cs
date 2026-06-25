using Microsoft.AspNetCore.Identity;
using ltwnc.Repositories;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ tài khoản — sử dụng ASP.NET Identity
public class AccountService : IAccountService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    // Inject UserManager (quản lý user) và SignInManager (quản lý đăng nhập)
    public AccountService(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // Đăng ký tài khoản mới
    // Tạo đối tượng IdentityUser → gọi UserManager.CreateAsync để hash password và lưu DB
    public async Task<IdentityResult> RegisterAsync(string email, string username, string password)
    {
        var user = new IdentityUser { UserName = username, Email = email };
        return await _userManager.CreateAsync(user, password);
    }

    // Đăng nhập
    // Tìm user theo email → xác thực mật khẩu qua SignInManager
    public async Task<SignInResult> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return SignInResult.Failed;
        return await _signInManager.PasswordSignInAsync(user, password, rememberMe, false);
    }

    // Đăng xuất — xóa cookie xác thực
    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    // Lấy thông tin người dùng hiện tại từ ClaimsPrincipal
    // ClaimsPrincipal được tạo từ cookie khi người dùng đăng nhập
    public async Task<IdentityUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        return await _userManager.GetUserAsync(principal);
    }
}
