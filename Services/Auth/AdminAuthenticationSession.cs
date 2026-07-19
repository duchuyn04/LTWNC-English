using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Services.Auth;

public sealed class AdminAuthenticationSession
{
    public const string VerifiedAtClaim = "ltwnc:admin:two_factor_verified_at";
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly TimeProvider _timeProvider;

    public AdminAuthenticationSession(
        SignInManager<IdentityUser> signInManager,
        TimeProvider timeProvider)
    {
        _signInManager = signInManager;
        _timeProvider = timeProvider;
    }

    public async Task SignInVerifiedAsync(
        IdentityUser user,
        AuthenticationProperties authenticationProperties)
    {
        string verifiedAt = _timeProvider.GetUtcNow()
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        await _signInManager.SignInWithClaimsAsync(
            user,
            authenticationProperties,
            [
                new Claim(VerifiedAtClaim, verifiedAt),
                new Claim("amr", "mfa")
            ]);
    }

    public static bool IsRecent(
        ClaimsPrincipal principal,
        DateTimeOffset now,
        TimeSpan maximumAge)
    {
        string? claimValue = principal.FindFirstValue(VerifiedAtClaim);
        if (!long.TryParse(
                claimValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long verifiedAtUnix))
        {
            return false;
        }

        TimeSpan age = now - DateTimeOffset.FromUnixTimeSeconds(verifiedAtUnix);
        return age >= TimeSpan.Zero && age <= maximumAge;
    }

    public static Task PreserveVerificationClaimsAsync(
        SecurityStampRefreshingPrincipalContext context)
    {
        ClaimsPrincipal? currentPrincipal = context.CurrentPrincipal;
        ClaimsPrincipal? newPrincipal = context.NewPrincipal;
        if (currentPrincipal == null
            || newPrincipal?.Identity is not ClaimsIdentity newIdentity)
        {
            return Task.CompletedTask;
        }

        Claim? verifiedAt = currentPrincipal.FindFirst(VerifiedAtClaim);
        if (verifiedAt != null)
        {
            newIdentity.AddClaim(verifiedAt);
        }

        Claim? authenticationMethod = currentPrincipal.Claims.FirstOrDefault(
            claim => claim.Type == "amr" && claim.Value == "mfa");
        if (authenticationMethod != null)
        {
            newIdentity.AddClaim(authenticationMethod);
        }

        return Task.CompletedTask;
    }
}

public sealed record RecentAdminAuthenticationRequirement(TimeSpan MaximumAge)
    : IAuthorizationRequirement;

public sealed record AdminTwoFactorEnabledRequirement : IAuthorizationRequirement;

public sealed class AdminTwoFactorEnabledHandler
    : AuthorizationHandler<AdminTwoFactorEnabledRequirement>
{
    private readonly UserManager<IdentityUser> _userManager;

    public AdminTwoFactorEnabledHandler(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminTwoFactorEnabledRequirement requirement)
    {
        IdentityUser? user = await _userManager.GetUserAsync(context.User);
        if (user != null && await _userManager.GetTwoFactorEnabledAsync(user))
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class RecentAdminAuthenticationHandler
    : AuthorizationHandler<RecentAdminAuthenticationRequirement>
{
    private readonly TimeProvider _timeProvider;

    public RecentAdminAuthenticationHandler(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RecentAdminAuthenticationRequirement requirement)
    {
        if (AdminAuthenticationSession.IsRecent(
                context.User,
                _timeProvider.GetUtcNow(),
                requirement.MaximumAge))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
