using System.Globalization;
using System.Security.Claims;
using ltwnc.Services.Auth;

namespace ltwnc.Tests.Services.Auth;

public sealed class AdminAuthenticationSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 19, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public void IsRecent_AtFifteenMinuteBoundary_ReturnsTrue()
    {
        ClaimsPrincipal principal = CreatePrincipal(Now.AddMinutes(-15));

        bool result = AdminAuthenticationSession.IsRecent(
            principal,
            Now,
            TimeSpan.FromMinutes(15));

        Assert.True(result);
    }

    [Fact]
    public void IsRecent_OlderThanFifteenMinutes_ReturnsFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal(Now.AddMinutes(-15).AddSeconds(-1));

        bool result = AdminAuthenticationSession.IsRecent(
            principal,
            Now,
            TimeSpan.FromMinutes(15));

        Assert.False(result);
    }

    [Fact]
    public void IsRecent_FutureTimestamp_ReturnsFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal(Now.AddSeconds(1));

        bool result = AdminAuthenticationSession.IsRecent(
            principal,
            Now,
            TimeSpan.FromMinutes(15));

        Assert.False(result);
    }

    private static ClaimsPrincipal CreatePrincipal(DateTimeOffset verifiedAt) =>
        new(new ClaimsIdentity(
            [
                new Claim(
                    AdminAuthenticationSession.VerifiedAtClaim,
                    verifiedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
            ],
            "Test"));
}
