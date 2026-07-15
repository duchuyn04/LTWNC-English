namespace ltwnc.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string userName, string email, string password);

    Task<AuthResult> LoginAsync(string email, string password, bool rememberMe);

    Task LogoutAsync();
}
