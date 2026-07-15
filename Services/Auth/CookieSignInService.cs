using System.Security.Claims;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.Auth;

// Ghi/xóa cookie authentication (claims NameIdentifier, Name, Email).
public class CookieSignInService : ISignInService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieSignInService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SignInAsync(AppUser user, bool rememberMe, TimeSpan cookieLifetime)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        // Always persist the auth cookie (matches old AccountController).
        // rememberMe only affects lifetime via cookieLifetime (30d vs 1d).
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(cookieLifetime)
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }

    public async Task SignOutAsync()
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
