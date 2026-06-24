using Microsoft.AspNetCore.Identity;

namespace ltwnc.Services;

public interface IAccountService
{
    Task<IdentityResult> RegisterAsync(string email, string username, string password);
    Task<SignInResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
    Task<IdentityUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal);
}
