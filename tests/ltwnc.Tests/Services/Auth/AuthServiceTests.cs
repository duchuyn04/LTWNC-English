using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Auth;

public class AuthServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AuthService CreateService(AppDbContext db, AdjustableTimeProvider clock)
    {
        return new AuthService(
            db,
            new PasswordHasher<AppUser>(),
            new HttpContextAccessor(),
            clock);
    }

    [Fact]
    public async Task RegisterAsync_ValidInput_CreatesUserAndProfile()
    {
        using AppDbContext db = CreateContext();
        var clock = new AdjustableTimeProvider();
        AuthService service = CreateService(db, clock);

        AuthResult result = await service.RegisterAsync("a@example.com", "alice", "Password1");

        Assert.True(result.Succeeded);
        AppUser? user = await db.AppUsers.SingleOrDefaultAsync(u => u.NormalizedEmail == "A@EXAMPLE.COM");
        Assert.NotNull(user);
        Assert.Equal("ALICE", user.NormalizedUserName);
        Assert.True(await db.UserProfiles.AnyAsync(p => p.UserId == user.Id));
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsDuplicateEmailError()
    {
        using AppDbContext db = CreateContext();
        AuthService service = CreateService(db, new AdjustableTimeProvider());
        await service.RegisterAsync("a@example.com", "alice", "Password1");

        AuthResult result = await service.RegisterAsync("A@example.com", "bob", "Password1");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task RegisterAsync_WeakPassword_ReturnsPolicyError()
    {
        using AppDbContext db = CreateContext();
        AuthService service = CreateService(db, new AdjustableTimeProvider());

        AuthResult result = await service.RegisterAsync("a@example.com", "alice", "short");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "PasswordTooShort");
        Assert.Empty(db.AppUsers);
    }

    [Fact]
    public async Task ValidateLoginAsync_WrongPasswordFiveTimes_LocksOutFor15Minutes()
    {
        using AppDbContext db = CreateContext();
        var clock = new AdjustableTimeProvider();
        AuthService service = CreateService(db, clock);
        await service.RegisterAsync("a@example.com", "alice", "Password1");
        AppUser user = await db.AppUsers.SingleAsync();

        for (int i = 0; i < AuthService.MaxFailedAccessAttempts - 1; i++)
        {
            AuthResult attempt = await service.ValidateLoginAsync(user, "Wrongpass1");
            Assert.False(attempt.Succeeded);
            Assert.False(attempt.IsLockedOut);
        }

        AuthResult fifth = await service.ValidateLoginAsync(user, "Wrongpass1");
        Assert.True(fifth.IsLockedOut);

        AuthResult duringLock = await service.ValidateLoginAsync(user, "Password1");
        Assert.True(duringLock.IsLockedOut);
    }

    [Fact]
    public async Task ValidateLoginAsync_CorrectPassword_ResetsFailedCount()
    {
        using AppDbContext db = CreateContext();
        AuthService service = CreateService(db, new AdjustableTimeProvider());
        await service.RegisterAsync("a@example.com", "alice", "Password1");
        AppUser user = await db.AppUsers.SingleAsync();

        await service.ValidateLoginAsync(user, "Wrongpass1");
        AuthResult result = await service.ValidateLoginAsync(user, "Password1");

        Assert.True(result.Succeeded);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrent_ReturnsPasswordMismatch()
    {
        using AppDbContext db = CreateContext();
        AuthService service = CreateService(db, new AdjustableTimeProvider());
        await service.RegisterAsync("a@example.com", "alice", "Password1");
        AppUser user = await db.AppUsers.SingleAsync();

        AuthResult result = await service.ChangePasswordAsync(user, "Wrongpass1", "Newpass1");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "PasswordMismatch");
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_RotatesSecurityStamp()
    {
        using AppDbContext db = CreateContext();
        AuthService service = CreateService(db, new AdjustableTimeProvider());
        await service.RegisterAsync("a@example.com", "alice", "Password1");
        AppUser user = await db.AppUsers.SingleAsync();
        string oldStamp = user.SecurityStamp;

        AuthResult result = await service.ChangePasswordAsync(user, "Password1", "Newpass1");

        Assert.True(result.Succeeded);
        Assert.NotEqual(oldStamp, user.SecurityStamp);
        AuthResult login = await service.ValidateLoginAsync(user, "Newpass1");
        Assert.True(login.Succeeded);
    }
}
