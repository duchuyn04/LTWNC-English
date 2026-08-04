using ltwnc.Models.Entities;

namespace ltwnc.Services.Auth;

// Hợp đồng xử lý đăng ký, đăng nhập, cookie và thông tin bảo mật tài khoản.
public interface IAuthService
{
    // Tạo tài khoản local sau khi email đã được xác thực; passwordHash đã được tạo ở bước chờ OTP.
    Task<AuthResult> CreateVerifiedLocalUserAsync(string email, string userName, string passwordHash, CancellationToken cancellationToken = default);
    // Tạo tài khoản người dùng thường từ email Google đã xác thực.
    Task<AuthResult> CreateGoogleUserAsync(string email, string userNameCandidate, string googleSubjectId, CancellationToken cancellationToken = default);
    // Tìm tài khoản theo email đã chuẩn hóa.
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    // Tìm tài khoản theo username đã chuẩn hóa.
    Task<AppUser?> FindByUsernameAsync(string userName, CancellationToken cancellationToken = default);
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
    // Đặt lại mật khẩu sau khi đã xác thực OTP.
    Task<AuthResult> ResetPasswordAsync(AppUser user, string newPassword, CancellationToken cancellationToken = default);
    // Liên kết một Google subject sau khi người dùng đã xác minh quyền sở hữu tài khoản.
    Task<AuthResult> LinkGoogleAsync(AppUser user, string googleSubjectId, CancellationToken cancellationToken = default);
    // Đổi security stamp để thu hồi các phiên đăng nhập cũ.
    Task RotateSecurityStampAsync(AppUser user, CancellationToken cancellationToken = default);
}
