using Microsoft.AspNetCore.Identity;
using ltwnc.Repositories;

namespace ltwnc.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountService(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<IdentityResult> RegisterAsync(string email, string username, string password)
    {
        var user = new IdentityUser { UserName = username, Email = email };
        return await _userManager.CreateAsync(user, password);
    }

    public async Task<SignInResult> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return SignInResult.Failed;
        return await _signInManager.PasswordSignInAsync(user, password, rememberMe, false);
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<IdentityUser?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        return await _userManager.GetUserAsync(principal);
    }
}
