using System.Security.Claims;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Profiles;
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
        // 1. Lưu dependency `_db` để các phương thức khác sử dụng.
        _db = db;
        // 2. Lưu dependency `_passwordHasher` để các phương thức khác sử dụng.
        _passwordHasher = passwordHasher;
        // 3. Lưu dependency `_httpContextAccessor` để các phương thức khác sử dụng.
        _httpContextAccessor = httpContextAccessor;
        // 4. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    public async Task<AuthResult> CreateVerifiedLocalUserAsync(
        string email,
        string userName,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        return await CreateUserAsync(
            email,
            userName,
            passwordHash,
            googleSubjectId: null,
            cancellationToken);
    }

    public async Task<AuthResult> CreateGoogleUserAsync(
        string email,
        string userNameCandidate,
        string googleSubjectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleSubjectId))
        {
            return AuthResult.Failure(new AuthError("GoogleIdentityMissing", "Tài khoản Google không hợp lệ."));
        }

        if (await _db.AppUsers.AnyAsync(
                user => user.GoogleSubjectId == googleSubjectId,
                cancellationToken))
        {
            return AuthResult.Failure(new AuthError("GoogleAlreadyLinked", "Tài khoản Google đã được liên kết."));
        }

        string? userName = await FindAvailableUserNameAsync(
            userNameCandidate.Trim(),
            cancellationToken);
        if (userName == null)
        {
            return AuthResult.Failure(new AuthError("InvalidUserName", "Không thể tạo tên đăng nhập từ tài khoản Google."));
        }

        return await CreateUserAsync(
            email,
            userName,
            passwordHash: string.Empty,
            googleSubjectId,
            cancellationToken);
    }

    private async Task<AuthResult> CreateUserAsync(
        string email,
        string userName,
        string passwordHash,
        string? googleSubjectId,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();
        string normalizedUserName = userName.Trim().ToUpperInvariant();
        if (await _db.AppUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        if (await _db.AppUsers.AnyAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            return AuthResult.Failure(new AuthError("DuplicateUserName", "Tên đăng nhập đã được sử dụng."));
        }

        if (googleSubjectId != null && await _db.AppUsers.AnyAsync(
                user => user.GoogleSubjectId == googleSubjectId,
                cancellationToken))
        {
            return AuthResult.Failure(new AuthError("GoogleAlreadyLinked", "Tài khoản Google đã được liên kết."));
        }

        var user = new AppUser
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            UserName = userName.Trim(),
            NormalizedUserName = normalizedUserName,
            PasswordHash = passwordHash,
            GoogleSubjectId = googleSubjectId,
            EmailConfirmed = true
        };
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        _db.AppUsers.Add(user);
        _db.UserProfiles.Add(new UserProfile { UserId = user.Id, CreatedAt = now, UpdatedAt = now });
        _db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            UserId = user.Id,
            Amount = 10,
            BalanceAfter = 10,
            Type = CreditLedgerTypes.WelcomeBonus,
            SourceType = "UserRegistration",
            SourceId = user.Id,
            Description = "Tín dụng chào mừng",
            CreatedAtUtc = now
        });

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

    private async Task<string?> FindAvailableUserNameAsync(
        string candidate,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(UsernamePolicy.GetValidationError(candidate)))
        {
            return null;
        }

        for (int suffix = 0; suffix < 10_000; suffix++)
        {
            string value = suffix == 0 ? candidate : $"{candidate}{suffix + 1}";
            if (!await _db.AppUsers.AnyAsync(
                    user => user.NormalizedUserName == value.ToUpperInvariant(),
                    cancellationToken))
            {
                return value;
            }
        }

        return null;
    }

    public async Task<AppUser?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ToUpperInvariant` và lưu kết quả vào `normalizedEmail`.
        string normalizedEmail = email.Trim().ToUpperInvariant();
        // 2. Trả kết quả từ `SingleOrDefaultAsync` cho nơi gọi.
        return await _db.AppUsers
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<AppUser?> FindByUsernameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserName = userName.Trim().ToUpperInvariant();
        return await _db.AppUsers
            .SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);
    }

    public async Task<AppUser?> FindByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Trả kết quả từ `SingleOrDefaultAsync` cho nơi gọi.
        return await _db.AppUsers
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<AuthResult> ValidateLoginAsync(
        AppUser user,
        string password,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        // 2. Kiểm tra `user.LockoutEnd.HasValue && user.LockoutEnd.Value > now` để chọn nhánh xử lý phù hợp.
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > now)
        {
            // 3. Trả kết quả từ `LockedOut` cho nơi gọi.
            return AuthResult.LockedOut();
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return AuthResult.Failure(new AuthError("InvalidCredentials", "Tên đăng nhập hoặc mật khẩu không đúng."));
        }

        PasswordVerificationResult verification =
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        // 5. Kiểm tra `verification == PasswordVerificationResult.Failed` để chọn nhánh xử lý phù hợp.
        if (verification == PasswordVerificationResult.Failed)
        {
            // 6. Cập nhật bộ đếm hoặc trạng thái `user.AccessFailedCount`.
            user.AccessFailedCount++;
            // 7. Kiểm tra `user.AccessFailedCount >= MaxFailedAccessAttempts` để chọn nhánh xử lý phù hợp.
            if (user.AccessFailedCount >= MaxFailedAccessAttempts)
            {
                // 8. Cập nhật `user.LockoutEnd` bằng giá trị mới.
                user.LockoutEnd = now.Add(LockoutDuration);
                // 9. Cập nhật `user.AccessFailedCount` bằng giá trị mới.
                user.AccessFailedCount = 0;
            }

            // 10. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _db.SaveChangesAsync(cancellationToken);
            // 11. Trả `user.LockoutEnd.HasValue && user.LockoutEnd.Value > now ? AuthResul...` cho nơi gọi.
            return user.LockoutEnd.HasValue && user.LockoutEnd.Value > now
                ? AuthResult.LockedOut()
                : AuthResult.Failure(new AuthError("InvalidCredentials", "Tên đăng nhập hoặc mật khẩu không đúng."));
        }

        // 12. Cập nhật `user.AccessFailedCount` bằng giá trị mới.
        user.AccessFailedCount = 0;
        // 13. Kiểm tra `verification == PasswordVerificationResult.SuccessRehashNeeded` để chọn nhánh xử lý phù hợp.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // 14. Cập nhật `user.PasswordHash` bằng giá trị mới.
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        // 15. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _db.SaveChangesAsync(cancellationToken);
        // 16. Trả kết quả từ `Success` cho nơi gọi.
        return AuthResult.Success();
    }

    public async Task SignInAsync(AppUser user, TimeSpan lifetime)
    {
        // 1. Tính giá trị và lưu vào `httpContext` để dùng ở bước tiếp theo.
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext để đăng nhập.");

        // 2. Gọi `SignInAsync` để thực hiện bước nghiệp vụ này.
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
        // 1. Tính giá trị và lưu vào `httpContext` để dùng ở bước tiếp theo.
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext để đăng xuất.");

        // 2. Gọi `SignOutAsync` để thực hiện bước nghiệp vụ này.
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task RefreshSignInAsync(AppUser user)
    {
        // 1. Tính giá trị và lưu vào `httpContext` để dùng ở bước tiếp theo.
        HttpContext httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Không có HttpContext để làm mới phiên.");

        // 2. Gọi `AuthenticateAsync` và lưu kết quả vào `current`.
        AuthenticateResult current =
            await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // 3. Tính giá trị và lưu vào `properties` để dùng ở bước tiếp theo.
        AuthenticationProperties properties = current.Properties ?? new AuthenticationProperties();
        // 4. Gọi `SignInAsync` để thực hiện bước nghiệp vụ này.
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
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return AuthResult.Failure(new AuthError("PasswordMismatch", "Tài khoản chưa có mật khẩu ứng dụng. Hãy dùng OTP để tạo mật khẩu."));
        }

        PasswordVerificationResult verification =
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        // 2. Kiểm tra `verification == PasswordVerificationResult.Failed` để chọn nhánh xử lý phù hợp.
        if (verification == PasswordVerificationResult.Failed)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AuthResult.Failure(new AuthError("PasswordMismatch", "Mật khẩu hiện tại không đúng."));
        }

        // 4. Gọi `GetValidationError` và lưu kết quả vào `policyError`.
        AuthError? policyError = PasswordPolicy.GetValidationError(newPassword);
        // 5. Kiểm tra `policyError != null` để chọn nhánh xử lý phù hợp.
        if (policyError != null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return AuthResult.Failure(policyError);
        }

        // 7. Cập nhật `user.PasswordHash` bằng giá trị mới.
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        // 8. Cập nhật `user.SecurityStamp` bằng giá trị mới.
        user.SecurityStamp = Guid.NewGuid().ToString();
        // 9. Cập nhật `user.ConcurrencyStamp` bằng giá trị mới.
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        // 10. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _db.SaveChangesAsync(cancellationToken);
        return AuthResult.Success();
    }

    public async Task<AuthResult> ResetPasswordAsync(
        AppUser user,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        AuthError? policyError = PasswordPolicy.GetValidationError(newPassword);
        if (policyError != null)
        {
            return AuthResult.Failure(policyError);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.EmailConfirmed = true;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await _db.SaveChangesAsync(cancellationToken);
        return AuthResult.Success();
    }

    public async Task<AuthResult> LinkGoogleAsync(
        AppUser user,
        string googleSubjectId,
        CancellationToken cancellationToken = default)
    {
        if (user.IsAdmin || string.IsNullOrWhiteSpace(googleSubjectId))
        {
            return AuthResult.Failure(new AuthError("GoogleLinkDenied", "Không thể liên kết tài khoản Google."));
        }

        AppUser? linkedUser = await _db.AppUsers
            .SingleOrDefaultAsync(item => item.GoogleSubjectId == googleSubjectId, cancellationToken);
        if (linkedUser != null && linkedUser.Id != user.Id)
        {
            return AuthResult.Failure(new AuthError("GoogleAlreadyLinked", "Tài khoản Google đã được liên kết."));
        }

        user.GoogleSubjectId = googleSubjectId;
        user.EmailConfirmed = true;
        await _db.SaveChangesAsync(cancellationToken);
        return AuthResult.Success();
    }

    public async Task RotateSecurityStampAsync(
        AppUser user,
        CancellationToken cancellationToken = default)
    {
        // 1. Cập nhật `user.SecurityStamp` bằng giá trị mới.
        user.SecurityStamp = Guid.NewGuid().ToString();
        // 2. Cập nhật `user.ConcurrencyStamp` bằng giá trị mới.
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        // 3. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ClaimsPrincipal BuildPrincipal(AppUser user)
    {
        // 1. Khởi tạo `claims` với dữ liệu ban đầu cần thiết.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp)
        };
        // 2. Kiểm tra `user.IsAdmin` để chọn nhánh xử lý phù hợp.
        if (user.IsAdmin)
        {
            // 3. Gọi `Add` để thực hiện bước nghiệp vụ này.
            claims.Add(new Claim(AppClaimTypes.IsAdmin, "true"));
        }

        // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static bool IsDuplicateViolation(DbUpdateException exception)
    {
        // 1. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
        string message = exception.InnerException?.Message ?? exception.Message;
        // 2. Trả `message.Contains("AppUsers", StringComparison.OrdinalIgnoreCase) &&...` cho nơi gọi.
        return message.Contains("AppUsers", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
