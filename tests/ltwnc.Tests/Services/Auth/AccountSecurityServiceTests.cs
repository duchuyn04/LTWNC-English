using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Auth;

public sealed class AccountSecurityServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RegistrationOtpActivatesUserOnlyAfterCorrectCode()
    {
        await using AppDbContext context = CreateContext();
        CapturingEmailSender sender = new();
        AccountSecurityService security = CreateSecurity(context, sender);

        RegistrationStartResult start = await security.StartLocalRegistrationAsync(
            "learner@example.com",
            "learner",
            "Password1",
            "127.0.0.1");

        Assert.True(start.Succeeded);
        Assert.Empty(context.AppUsers);
        Assert.NotNull(start.ChallengeId);
        Assert.Contains("123456", sender.LastBody, StringComparison.Ordinal);

        AccountSecurityResult verification = await security.VerifyRegistrationAsync(
            start.ChallengeId!,
            "123456");

        AppUser user = await context.AppUsers.SingleAsync();
        Assert.True(verification.Succeeded);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("learner@example.com", user.Email);
        Assert.Empty(context.PendingRegistrations);
    }

    [Fact]
    public async Task RegistrationOtpIsInvalidAfterThreeWrongAttempts()
    {
        await using AppDbContext context = CreateContext();
        AccountSecurityService security = CreateSecurity(context, new CapturingEmailSender());

        RegistrationStartResult start = await security.StartLocalRegistrationAsync(
            "learner@example.com",
            "learner",
            "Password1",
            "127.0.0.1");

        for (int attempt = 0; attempt < 3; attempt++)
        {
            AccountSecurityResult result = await security.VerifyRegistrationAsync(
                start.ChallengeId!,
                "000000");
            Assert.False(result.Succeeded);
        }

        AccountSecurityResult afterLockout = await security.VerifyRegistrationAsync(
            start.ChallengeId!,
            "123456");

        Assert.False(afterLockout.Succeeded);
        Assert.Empty(context.AppUsers);
    }

    [Fact]
    public async Task GoogleUsersGetUniqueUsernamesAndCanUsePasswordResetLater()
    {
        await using AppDbContext context = CreateContext();
        AuthService auth = CreateAuth(context);

        Assert.True((await auth.CreateGoogleUserAsync(
            "john@example.com",
            "john",
            "google-1")).Succeeded);
        Assert.True((await auth.CreateGoogleUserAsync(
            "john@other.example",
            "john",
            "google-2")).Succeeded);

        AppUser first = await context.AppUsers.SingleAsync(user => user.Email == "john@example.com");
        AppUser second = await context.AppUsers.SingleAsync(user => user.Email == "john@other.example");
        Assert.Equal("john", first.UserName);
        Assert.Equal("john2", second.UserName);
        Assert.False((await auth.ValidateLoginAsync(first, "Password1")).Succeeded);
    }

    [Fact]
    public async Task AdminCanLinkGoogleAccountViaOtp()
    {
        await using AppDbContext context = CreateContext();
        CapturingEmailSender sender = new();
        AccountSecurityService security = CreateSecurity(context, sender);
        AuthService auth = CreateAuth(context);

        string passwordHash = new PasswordHasher<AppUser>().HashPassword(
            new AppUser { Email = "admin@example.com" },
            "Password1");
        Assert.True((await auth.CreateVerifiedLocalUserAsync(
            "admin@example.com",
            "admin",
            passwordHash)).Succeeded);
        AppUser admin = await context.AppUsers.SingleAsync();
        admin.IsAdmin = true;
        await context.SaveChangesAsync();

        RegistrationStartResult start = await security.StartGoogleLinkOtpAsync(
            admin.Id,
            "google-admin-1",
            "127.0.0.1");

        Assert.True(start.Succeeded);
        Assert.NotNull(start.ChallengeId);

        AccountSecurityResult result = await security.CompleteGoogleLinkOtpAsync(
            start.ChallengeId!,
            "123456");

        Assert.True(result.Succeeded);
        Assert.Equal(admin.Id, result.UserId);
        Assert.Equal("google-admin-1", admin.GoogleSubjectId);
    }

    [Fact]
    public async Task PasswordResetReportsEmailFailureWithoutLeavingChallenge()
    {
        await using AppDbContext context = CreateContext();
        AuthService auth = CreateAuth(context);
        Assert.True((await auth.CreateVerifiedLocalUserAsync(
            "learner@example.com",
            "learner",
            new PasswordHasher<AppUser>().HashPassword(
                new AppUser { Email = "learner@example.com" },
                "Password1"))).Succeeded);

        AccountSecurityService security = CreateSecurity(context, new FailingEmailSender());

        PasswordResetStartResult result = await security.StartPasswordResetAsync(
            "learner@example.com",
            "127.0.0.1");

        Assert.False(result.Succeeded);
        Assert.Contains("email", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.EmailOtpChallenges);
    }

    [Fact]
    public async Task PasswordResetChangesPasswordAndRevokesOldSecurityStamp()
    {
        await using AppDbContext context = CreateContext();
        CapturingEmailSender sender = new();
        AccountSecurityService security = CreateSecurity(context, sender);
        AuthService auth = CreateAuth(context);

        string passwordHash = new PasswordHasher<AppUser>().HashPassword(
            new AppUser { Email = "learner@example.com" },
            "Password1");
        Assert.True((await auth.CreateVerifiedLocalUserAsync(
            "learner@example.com",
            "learner",
            passwordHash)).Succeeded);
        AppUser user = await context.AppUsers.SingleAsync();
        string oldSecurityStamp = user.SecurityStamp;

        PasswordResetStartResult start = await security.StartPasswordResetAsync(
            "learner@example.com",
            "127.0.0.1");
        Assert.NotNull(start.ChallengeId);

        AccountSecurityResult result = await security.CompletePasswordResetAsync(
            start.ChallengeId!,
            "123456",
            "NewPassword1");

        Assert.True(result.Succeeded);
        Assert.NotEqual(oldSecurityStamp, user.SecurityStamp);
        Assert.True((await auth.ValidateLoginAsync(user, "NewPassword1")).Succeeded);
    }

    private static AccountSecurityService CreateSecurity(
        AppDbContext context,
        IEmailMessageSender sender)
    {
        AuthService auth = CreateAuth(context);
        IPasswordHasher<AppUser> hasher = new PasswordHasher<AppUser>();
        EmailOtpService otp = new(
            context,
            sender,
            hasher,
            new FixedOtpCodeGenerator(),
            new FixedTimeProvider(FixedNow));
        return new AccountSecurityService(
            context,
            auth,
            otp,
            hasher,
            new FixedTimeProvider(FixedNow));
    }

    private static AuthService CreateAuth(AppDbContext context)
    {
        return new AuthService(
            context,
            new PasswordHasher<AppUser>(),
            new HttpContextAccessor(),
            new FixedTimeProvider(FixedNow));
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class CapturingEmailSender : IEmailMessageSender
    {
        public string LastBody { get; private set; } = string.Empty;

        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            LastBody = body;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailMessageSender
    {
        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("SMTP failed");
        }
    }

    private sealed class FixedOtpCodeGenerator : IOtpCodeGenerator
    {
        public string Generate() => "123456";
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
