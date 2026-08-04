using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.Credits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ltwnc.Tests.Services.Auth;

public sealed class AuthCreditTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 1, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task VerifiedRegistration_GrantsWelcomeCreditsVisibleThroughCreditService()
    {
        await using AppDbContext context = CreateContext();
        AuthService auth = new(
            context,
            new PasswordHasher<AppUser>(),
            new HttpContextAccessor(),
            new FixedTimeProvider(FixedNow));

        PasswordHasher<AppUser> hasher = new();
        string passwordHash = hasher.HashPassword(
            new AppUser { Email = "learner@example.com" },
            "Password1");
        AuthResult result = await auth.CreateVerifiedLocalUserAsync(
            "learner@example.com",
            "learner",
            passwordHash);

        AppUser user = await context.AppUsers.SingleAsync();
        CreditLedgerEntry entry = await context.CreditLedgerEntries.SingleAsync();
        CreditService credits = new(context, new ConfigurationBuilder().Build(), new FixedTimeProvider(FixedNow));

        Assert.True(result.Succeeded);
        Assert.Equal(10, await credits.GetBalanceAsync(user.Id));
        Assert.Equal(10, entry.Amount);
        Assert.Equal(10, entry.BalanceAfter);
        Assert.Equal(CreditLedgerTypes.WelcomeBonus, entry.Type);
        Assert.Equal("UserRegistration", entry.SourceType);
        Assert.Equal(user.Id, entry.SourceId);
        Assert.Equal(FixedNow.UtcDateTime, entry.CreatedAtUtc);
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
