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
        // 1. Lưu dependency `_db` để các phương thức khác sử dụng.
        _db = db;
        // 2. Lưu dependency `_passwordHasher` để các phương thức khác sử dụng.
        _passwordHasher = passwordHasher;
        // 3. Lưu dependency `_httpContextAccessor` để các phương thức khác sử dụng.
        _httpContextAccessor = httpContextAccessor;
        // 4. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    public async Task<AuthResult> RegisterAsync(
        string email,
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetValidationError` và lưu kết quả vào `passwordError`.
        AuthError? passwordError = PasswordPolicy.GetValidationError(password);
        // 2. Kiểm tra `passwordError != null` để chọn nhánh xử lý phù hợp.
        if (passwordError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AuthResult.Failure(passwordError);
        }

        // 4. Gọi `ToUpperInvariant` và lưu kết quả vào `normalizedEmail`.
        string normalizedEmail = email.ToUpperInvariant();
        // 5. Gọi `ToUpperInvariant` và lưu kết quả vào `normalizedUserName`.
        string normalizedUserName = userName.ToUpperInvariant();

        // 6. Kiểm tra `await _db.AppUsers.AnyAsync(user => user.NormalizedEmail == normali...` để chọn nhánh xử lý phù hợp.
        if (await _db.AppUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            // 7. Trả kết quả từ `Failure` cho nơi gọi.
            return AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        // 8. Kiểm tra `await _db.AppUsers.AnyAsync(user => user.NormalizedUserName == norm...` để chọn nhánh xử lý phù hợp.
        if (await _db.AppUsers.AnyAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            // 9. Trả kết quả từ `Failure` cho nơi gọi.
            return AuthResult.Failure(new AuthError("DuplicateUserName", "Tên đăng nhập đã được sử dụng."));
        }

        // 10. Khởi tạo `user` với dữ liệu ban đầu cần thiết.
        var user = new AppUser
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            UserName = userName,
            NormalizedUserName = normalizedUserName
        };
        // 11. Cập nhật `user.PasswordHash` bằng giá trị mới.
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        // 12. Tính giá trị và lưu vào `now` để dùng ở bước tiếp theo.
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        // 13. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _db.AppUsers.Add(user);
        // 14. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 15. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 16. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateViolation(exception))
        {
            // 17. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _db.ChangeTracker.Clear();
            // 18. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            string message = exception.InnerException?.Message ?? exception.Message;
            // 19. Trả `message.Contains("AppUserNameIndex", StringComparison.OrdinalIgnore...` cho nơi gọi.
            return message.Contains("AppUserNameIndex", StringComparison.OrdinalIgnoreCase)
                ? AuthResult.Failure(new AuthError("DuplicateUserName", "Tên đăng nhập đã được sử dụng."))
                : AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        // 20. Trả kết quả từ `Success` cho nơi gọi.
        return AuthResult.Success();
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

        // 4. Gọi `VerifyHashedPassword` và lưu kết quả vào `verification`.
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
                : AuthResult.Failure(new AuthError("InvalidCredentials", "Email hoặc mật khẩu không đúng."));
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
        // 1. Gọi `VerifyHashedPassword` và lưu kết quả vào `verification`.
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
        // 11. Trả kết quả từ `Success` cho nơi gọi.
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
