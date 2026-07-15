using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ltwnc.Tests.Services.Auth;

// Test AuthService với EF InMemory + mock ISignInService (không cần cookie pipeline).
public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly Mock<ISignInService> _signIn = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        // DbContext test kế thừa AppDbContext và đăng ký AppUser (Task 3 mới thêm DbSet Users).
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AuthTestDbContext(options);
        _auth = new AuthService(_db, _hasher, _signIn.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Register_creates_user_and_calls_sign_in()
    {
        AuthResult result = await _auth.RegisterAsync("alice", "Alice@Example.com", "Secret1A");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);

        AppUser? stored = await _db.Set<AppUser>().SingleOrDefaultAsync();
        Assert.NotNull(stored);
        Assert.Equal("alice", stored.UserName);
        Assert.Equal("alice@example.com", stored.Email);
        Assert.True(_hasher.Verify("Secret1A", stored.PasswordHash));

        _signIn.Verify(
            s => s.SignInAsync(
                It.Is<AppUser>(u => u.Email == "alice@example.com" && u.UserName == "alice"),
                true,
                TimeSpan.FromDays(1)),
            Times.Once);
    }

    [Fact]
    public async Task Register_duplicate_email_fails()
    {
        _db.Set<AppUser>().Add(new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "existing",
            Email = "taken@example.com",
            PasswordHash = _hasher.Hash("Secret1A")
        });
        await _db.SaveChangesAsync();

        AuthResult result = await _auth.RegisterAsync("newbie", "taken@example.com", "Secret1A");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Email", StringComparison.OrdinalIgnoreCase));
        _signIn.Verify(
            s => s.SignInAsync(It.IsAny<AppUser>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_wrong_password_fails_without_sign_in()
    {
        _db.Set<AppUser>().Add(new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "bob",
            Email = "bob@example.com",
            PasswordHash = _hasher.Hash("Secret1A")
        });
        await _db.SaveChangesAsync();

        AuthResult result = await _auth.LoginAsync("bob@example.com", "Wrong999", rememberMe: false);

        Assert.False(result.Succeeded);
        Assert.Contains("Email hoặc mật khẩu không đúng.", result.Errors);
        _signIn.Verify(
            s => s.SignInAsync(It.IsAny<AppUser>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_success_calls_sign_in()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "carol",
            Email = "carol@example.com",
            PasswordHash = _hasher.Hash("Secret1A")
        };
        _db.Set<AppUser>().Add(user);
        await _db.SaveChangesAsync();

        AuthResult result = await _auth.LoginAsync("Carol@Example.com", "Secret1A", rememberMe: true);

        Assert.True(result.Succeeded);
        _signIn.Verify(
            s => s.SignInAsync(
                It.Is<AppUser>(u => u.Id == user.Id),
                true,
                TimeSpan.FromDays(30)),
            Times.Once);
    }

    // Context test: đăng ký AppUser vào model trước khi Task 3 thêm DbSet Users.
    private sealed class AuthTestDbContext : AppDbContext
    {
        public AuthTestDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.UserName).IsUnique();
            });
        }
    }
}
