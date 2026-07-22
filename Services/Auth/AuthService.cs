using System.Security.Claims;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Auth;

// Auth tự quản: hash mật khẩu, lockout, security stamp, phát/thu hồi cookie.
public sealed class AuthService : IAuthService
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<AppUser> passwordHasher,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<AuthResult> RegisterAsync(
        string email,
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        AuthError? passwordError = PasswordPolicy.GetValidationError(password);
        if (passwordError != null)
        {
            return AuthResult.Failure(passwordError);
        }

        string normalizedEmail = email.ToUpperInvariant();
        string normalizedUserName = userName.ToUpperInvariant();

        if (await _db.AppUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        if (await _db.AppUsers.AnyAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            return AuthResult.Failure(new AuthError("DuplicateUserName", "Tên đăng nhập đã được sử dụng."));
        }

        var user = new AppUser
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            UserName = userName,
            NormalizedUserName = normalizedUserName
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        _db.AppUsers.Add(user);
        _db.UserProfiles.Add(new UserProfile { UserId = user.Id, CreatedAt = now, UpdatedAt = now });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateViolation(exception))
        {
            _db.ChangeTracker.Clear();
            string message = exception.InnerException?.Message ?? exception.Message;
            return message.Contains("AppUserNameIndex", StringComparison.OrdinalIgnoreCase)
                ? AuthResult.Failure(new AuthError("DuplicateUserName", "Tên đăng nhập đã được sử dụng."))
                : AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        return AuthResult.Success();
    }

    public async Task<AppUser?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();
        return await _db.AppUsers
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<AppUser?> FindByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AppUsers
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<AuthResult> ValidateLoginAsync(
        AppUser user,
        string password,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > now)
        {
            return AuthResult.LockedOut();
        }

        PasswordVerificationResult verification =
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAccessAttempts)
            {
                user.LockoutEnd = now.Add(LockoutDuration);
                user.AccessFailedCount = 0;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return user.LockoutEnd.HasValue && user.LockoutEnd.Value > now
                ? AuthResult.LockedOut()
                : AuthResult.Failure(new AuthError("InvalidCredentials", "Email hoặc mật khẩu không đúng."));
        }

        user.AccessFailedCount = 0;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return AuthResult.Success();
    }

    public async Task SignInAsync(AppUser user, TimeSpan lifetime)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext để đăng nhập.");

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(user),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = _timeProvider.GetUtcNow().Add(lifetime)
            });
    }

    public async Task SignOutAsync()
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext để đăng xuất.");

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task RefreshSignInAsync(AppUser user)
    {
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext để làm mới phiên.");

        AuthenticateResult current =
            await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        AuthenticationProperties properties = current.Properties ?? new AuthenticationProperties();
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(user),
            properties);
    }

    public async Task<AuthResult> ChangePasswordAsync(
        AppUser user,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        PasswordVerificationResult verification =
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthResult.Failure(new AuthError("PasswordMismatch", "Mật khẩu hiện tại không đúng."));
        }

        AuthError? policyError = PasswordPolicy.GetValidationError(newPassword);
        if (policyError != null)
        {
            return AuthResult.Failure(policyError);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await _db.SaveChangesAsync(cancellationToken);
        return AuthResult.Success();
    }

    public async Task RotateSecurityStampAsync(
        AppUser user,
        CancellationToken cancellationToken = default)
    {
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ClaimsPrincipal BuildPrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp)
        };
        if (user.IsAdmin)
        {
            claims.Add(new Claim(AppClaimTypes.IsAdmin, "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static bool IsDuplicateViolation(DbUpdateException exception)
    {
        string message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("AppUsers", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
