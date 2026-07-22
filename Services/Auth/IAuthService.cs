using ltwnc.Models.Entities;

namespace ltwnc.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string userName, string password, CancellationToken cancellationToken = default);
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<AuthResult> ValidateLoginAsync(AppUser user, string password, CancellationToken cancellationToken = default);
    Task SignInAsync(AppUser user, TimeSpan lifetime);
    Task SignOutAsync();
    Task RefreshSignInAsync(AppUser user);
    Task<AuthResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task RotateSecurityStampAsync(AppUser user, CancellationToken cancellationToken = default);
}
