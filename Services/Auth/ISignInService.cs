using ltwnc.Models.Entities;

namespace ltwnc.Services.Auth;

// Tách cookie sign-in/out khỏi AuthService để unit test mock được.
public interface ISignInService
{
    Task SignInAsync(AppUser user, bool rememberMe, TimeSpan cookieLifetime);

    Task SignOutAsync();
}
