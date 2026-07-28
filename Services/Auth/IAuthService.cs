using ltwnc.Models.Entities;

namespace ltwnc.Services.Auth;

// Hợp đồng xử lý đăng ký, đăng nhập, cookie và thông tin bảo mật tài khoản.
public interface IAuthService
{
    // Tạo tài khoản mới sau khi kiểm tra email, username và mật khẩu.
    Task<AuthResult> RegisterAsync(string email, string userName, string password, CancellationToken cancellationToken = default);
    // Tìm tài khoản theo email đã chuẩn hóa.
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    // Tìm tài khoản theo mã định danh.
    Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);
    // Kiểm tra mật khẩu, số lần đăng nhập sai và trạng thái khóa.
    Task<AuthResult> ValidateLoginAsync(AppUser user, string password, CancellationToken cancellationToken = default);
    // Tạo cookie đăng nhập với thời hạn được chỉ định.
    Task SignInAsync(AppUser user, TimeSpan lifetime);
    // Xóa cookie của phiên đăng nhập hiện tại.
    Task SignOutAsync();
    // Phát lại cookie để cập nhật claim mới nhất.
    Task RefreshSignInAsync(AppUser user);
    // Đổi mật khẩu sau khi xác minh mật khẩu hiện tại.
    Task<AuthResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    // Đổi security stamp để thu hồi các phiên đăng nhập cũ.
    Task RotateSecurityStampAsync(AppUser user, CancellationToken cancellationToken = default);
}
