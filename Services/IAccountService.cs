using Microsoft.AspNetCore.Identity;

namespace ltwnc.Services;

// Interface định nghĩa các phương thức xử lý nghiệp vụ tài khoản
public interface IAccountService
{
    // Đăng ký tài khoản mới — trả về kết quả thành công hoặc lỗi
    Task<IdentityResult> RegisterAsync(string email, string username, string password);

    // Đăng nhập — trả về kết quả thành công hoặc thất bại
    Task<SignInResult> LoginAsync(string email, string password, bool rememberMe);

    // Đăng xuất
    Task LogoutAsync();

    // Lấy thông tin người dùng hiện tại từ ClaimsPrincipal (cookie auth)
    Task<IdentityUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal);
}
