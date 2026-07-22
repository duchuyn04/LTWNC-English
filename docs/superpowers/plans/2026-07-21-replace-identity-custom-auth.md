# Replace ASP.NET Identity with Custom Lightweight Auth — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Loại bỏ hoàn toàn ASP.NET Identity (UserManager/SignInManager/schema AspNet*) khỏi ltwnc, thay bằng auth tự quản tối giản: 1 bảng `AppUsers` + `AuthService`, tái dùng `PasswordHasher<T>` và cookie authentication middleware chuẩn của ASP.NET.

**Architecture:** Entity `AppUser` mới + `IAuthService`/`AuthService` (hash, lockout, security stamp, phát cookie). Cookie auth qua `AddAuthentication().AddCookie()` với scheme mặc định `Cookies`. Phân quyền admin bằng claim `IsAdmin=true` thay role. Reset dữ liệu auth (drop AspNet*), bỏ cơ chế AdminBootstrap.

**Tech Stack:** ASP.NET Core 10 (net10.0), EF Core 10 (SQL Server runtime, SQLite/InMemory cho tests), xunit + Moq, `Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>` (có sẵn trong shared framework, không cần thêm package).

**Spec:** `docs/superpowers/specs/2026-07-21-replace-identity-with-custom-auth-design.md`

**Branch:** `feature/replace-identity-custom-auth` (đã tạo, đang ở trên đó).

## Global Constraints

- Không thêm package NuGet mới. `PasswordHasher<T>` nằm trong `Microsoft.Extensions.Identity.Core` — có sẵn trong ASP.NET Core shared framework của `Microsoft.NET.Sdk.Web`.
- Claims cookie phải giữ: `ClaimTypes.NameIdentifier` = user Id, `ClaimTypes.Name` = username. Thêm `AppClaimTypes.SecurityStamp` và `AppClaimTypes.IsAdmin` (=`"true"` khi admin).
- Thông điệp lỗi tiếng Việt hiển thị phải giữ nguyên văn như hiện tại (xem từng task).
- Cookie: 1 ngày (register/login thường), 30 ngày (remember-me), sliding expiration, `LoginPath=/Account/Login`, AJAX (`X-Requested-With: XMLHttpRequest`) trả 401, `/Admin` bị cấm trả 403, validate principal mỗi request.
- Lockout: 5 lần sai → khóa 15 phút; admin khóa vĩnh viễn bằng `LockoutEnd = 9999-12-31`.
- Không tái hiện: password reset, email confirmation, 2FA, external login, token providers, AdminBootstrap.
- Mọi bảng FK `UserId` (string, maxlen 450) giữ nguyên kiểu — chỉ đổi principal sang `AppUsers`.
- Build + test chạy bằng: `dotnet build` và `dotnet test tests/ltwnc.Tests` từ thư mục gốc `C:/it/ltwnc`.

## Interface Contract (dùng xuyên suốt các task)

```csharp
// Models/Entities/AppUser.cs
namespace ltwnc.Models.Entities;

// Tài khoản ngườoi dùng tự quản — thay thế IdentityUser.
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public bool IsAdmin { get; set; }
}
```

```csharp
// Services/Auth/AppClaimTypes.cs
namespace ltwnc.Services.Auth;

public static class AppClaimTypes
{
    public const string IsAdmin = "IsAdmin";
    public const string SecurityStamp = "SecurityStamp";
}
```

```csharp
// Services/Auth/AuthResult.cs
namespace ltwnc.Services.Auth;

public sealed record AuthError(string Code, string Message);

public sealed class AuthResult
{
    public bool Succeeded { get; private init; }
    public bool IsLockedOut { get; private init; }
    public IReadOnlyList<AuthError> Errors { get; private init; } = [];

    public static AuthResult Success() => new() { Succeeded = true };
    public static AuthResult LockedOut() => new() { IsLockedOut = true };
    public static AuthResult Failure(params AuthError[] errors) => new() { Errors = errors };
}
```

```csharp
// Services/Auth/IAuthService.cs
using ltwnc.Models.Entities;

namespace ltwnc.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string userName, string password, CancellationToken cancellationToken = default);
    // Các hàm Find trả về tracked entity (KHÔNG AsNoTracking) để ValidateLoginAsync/ChangePasswordAsync ghi được.
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<AuthResult> ValidateLoginAsync(AppUser user, string password, CancellationToken cancellationToken = default);
    Task SignInAsync(AppUser user, TimeSpan lifetime);
    Task SignOutAsync();
    Task RefreshSignInAsync(AppUser user);
    Task<AuthResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task RotateSecurityStampAsync(AppUser user, CancellationToken cancellationToken = default);
}
```

Mã lỗi (`AuthError.Code`) dùng thống nhất: `DuplicateEmail`, `DuplicateUserName`, `PasswordTooShort`, `PasswordRequiresDigit`, `PasswordRequiresUpper`, `PasswordRequiresLower`, `PasswordMismatch`, `InvalidCredentials`.

---

### Task 1: Entity AppUser + DbSet + migration tạo bảng AppUsers

**Files:**
- Create: `Models/Entities/AppUser.cs`
- Modify: `Data/AppDbContext.cs` (thêm DbSet + cấu hình, CHƯA đổi base class)
- Create (tự sinh): `Migrations/<timestamp>_AddAppUsers.cs`

**Interfaces:**
- Consumes: không.
- Produces: `AppUser` (xem Interface Contract), `AppDbContext.AppUsers` (`DbSet<AppUser>`).

Giai đoạn này `AppDbContext` vẫn kế thừa `IdentityDbContext<IdentityUser>` — bảng AspNet* còn nguyên, app vẫn chạy bằng auth cũ. FK `UserProfile` CHƯA đổi (đổi ở Task 6) để không gãy luồng hiện tại.

- [ ] **Step 1: Tạo entity `AppUser`**

Tạo `Models/Entities/AppUser.cs` với đúng nội dung ở mục Interface Contract.

- [ ] **Step 2: Thêm DbSet + cấu hình vào `AppDbContext`**

Trong `Data/AppDbContext.cs`, thêm DbSet (đặt cạnh `UserProfiles`):

```csharp
public DbSet<AppUser> AppUsers => Set<AppUser>();
```

Thêm vào đầu `OnModelCreating` (ngay sau `base.OnModelCreating(builder);`):

```csharp
builder.Entity<AppUser>(entity =>
{
    entity.Property(user => user.Id).HasMaxLength(450);
    entity.Property(user => user.Email).HasMaxLength(256);
    entity.Property(user => user.NormalizedEmail).HasMaxLength(256);
    entity.Property(user => user.UserName).HasMaxLength(256);
    entity.Property(user => user.NormalizedUserName).HasMaxLength(256);
    entity.HasIndex(user => user.NormalizedEmail)
        .IsUnique()
        .HasDatabaseName("AppUserEmailIndex")
        .HasFilter("[NormalizedEmail] IS NOT NULL");
    entity.HasIndex(user => user.NormalizedUserName)
        .IsUnique()
        .HasDatabaseName("AppUserNameIndex")
        .HasFilter("[NormalizedUserName] IS NOT NULL");
});
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 4: Tạo migration**

Run: `dotnet ef migrations add AddAppUsers`
Expected: sinh `Migrations/<timestamp>_AddAppUsers.cs` chỉ chứa `CreateTable("AppUsers", ...)` + 2 index (không đụng bảng AspNet*).

- [ ] **Step 5: Chạy test hiện có để chứng minh không regression**

Run: `dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~AppDbContextTests"`
Expected: PASS (EnsureCreated trên SQLite giờ có thêm bảng AppUsers).

- [ ] **Step 6: Commit**

```bash
git add Models/Entities/AppUser.cs Data/AppDbContext.cs Migrations/
git commit -m "feat: them entity AppUser va migration tao bang AppUsers"
```

---

### Task 2: PasswordPolicy + AuthResult + AuthService (TDD)

**Files:**
- Create: `Services/Auth/AppClaimTypes.cs`, `Services/Auth/AuthResult.cs`, `Services/Auth/PasswordPolicy.cs`, `Services/Auth/IAuthService.cs`, `Services/Auth/AuthService.cs`
- Test: `tests/ltwnc.Tests/Services/Auth/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `AppUser`, `AppDbContext.AppUsers` (Task 1).
- Produces: toàn bộ Interface Contract ở đầu plan — các task sau chỉ được dùng đúng chữ ký này.

- [ ] **Step 1: Tạo các file contract**

Tạo `AppClaimTypes.cs`, `AuthResult.cs`, `IAuthService.cs` đúng nội dung ở mục Interface Contract.

Tạo `Services/Auth/PasswordPolicy.cs`:

```csharp
namespace ltwnc.Services.Auth;

// Chính sách mật khẩu giữ nguyên như Identity options cũ: >=8 ký tự, có số, hoa, thường.
public static class PasswordPolicy
{
    public const int RequiredLength = 8;

    public static AuthError? GetValidationError(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < RequiredLength)
        {
            return new AuthError("PasswordTooShort", "Mật khẩu phải có ít nhất 8 ký tự.");
        }

        if (!password.Any(char.IsDigit))
        {
            return new AuthError("PasswordRequiresDigit", "Mật khẩu phải có ít nhất một chữ số.");
        }

        if (!password.Any(char.IsUpper))
        {
            return new AuthError("PasswordRequiresUpper", "Mật khẩu phải có ít nhất một chữ hoa.");
        }

        if (!password.Any(char.IsLower))
        {
            return new AuthError("PasswordRequiresLower", "Mật khẩu phải có ít nhất một chữ thường.");
        }

        return null;
    }
}
```

- [ ] **Step 2: Viết failing test `AuthServiceTests`**

Tạo `tests/ltwnc.Tests/Services/Auth/AuthServiceTests.cs`:

```csharp
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
        Assert.Contains(result.Errors, e => e.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task RegisterAsync_WeakPassword_ReturnsPolicyError()
    {
        using AppDbContext db = CreateContext();
        AuthService service = CreateService(db, new AdjustableTimeProvider());

        AuthResult result = await service.RegisterAsync("a@example.com", "alice", "short");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordTooShort");
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

        // Trong thờoi gian khóa, kể cả đúng mật khẩu cũng bị từ chối.
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
        Assert.Contains(result.Errors, e => e.Code == "PasswordMismatch");
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
```

Kiểm tra `tests/ltwnc.Tests/Infrastructure/AdjustableTimeProvider.cs` có method tăng giờ (ví dụ `Advance`); nếu tên khác thì dùng đúng tên đó. Nếu chưa có method advance thì bỏ phần kiểm tra hết hạn khóa, chỉ giữ assert trong-thờoi-gian-khóa.

- [ ] **Step 3: Chạy test, xác nhận FAIL (chưa có AuthService)**

Run: `dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~AuthServiceTests"`
Expected: FAIL build — `AuthService` chưa tồn tại.

- [ ] **Step 4: Implement `Services/Auth/AuthService.cs`**

```csharp
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

        if (await _db.AppUsers.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        if (await _db.AppUsers.AnyAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken))
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
            // Race condition: unique index bắt trùng — thông báo giống kiểm tra phía trên.
            _db.ChangeTracker.Clear();
            string message = exception.InnerException?.Message ?? exception.Message;
            return message.Contains("AppUserNameIndex", StringComparison.OrdinalIgnoreCase)
                ? AuthResult.Failure(new AuthError("DuplicateUserName", "Tên đăng nhập đã được sử dụng."))
                : AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng."));
        }

        return AuthResult.Success();
    }

    public async Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();
        return await _db.AppUsers
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.AppUsers
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
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

    public async Task RotateSecurityStampAsync(AppUser user, CancellationToken cancellationToken = default)
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
```

- [ ] **Step 5: Chạy test, xác nhận PASS**

Run: `dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~AuthServiceTests"`
Expected: 7/7 PASS.

- [ ] **Step 6: Commit**

```bash
git add Services/Auth/ tests/ltwnc.Tests/Services/Auth/AuthServiceTests.cs
git commit -m "feat: them AuthService tu quan (hash, lockout, security stamp, cookie)"
```

---

### Task 3: Cookie auth trong Program.cs + rewire AccountController + test factory

**Files:**
- Modify: `Program.cs` (thêm cookie scheme mới + policy claim; GIỮ Identity DI đến Task 6)
- Modify: `Controllers/AccountController.cs` (toàn bộ)
- Modify: `tests/ltwnc.Tests/Infrastructure/AdminWebApplicationFactory.cs` (seed sang AppUsers)
- Test: `tests/ltwnc.Tests/Controllers/AccountControllerTests.cs` (viết lại)

**Interfaces:**
- Consumes: `IAuthService`, `AuthResult`, `AuthError`, `AppClaimTypes`, `AppUser` (Task 2).
- Produces: scheme `CookieAuthenticationDefaults.AuthenticationScheme` ("Cookies") là scheme mặc định toàn app; `AppDbContext` resolve được trong `OnValidatePrincipal`.

Lưu ý trạng thái trung gian: Identity DI (`AddIdentityCore...AddSignInManager` + `AddIdentityCookies`) VẪN GIỮ ở task này vì `ProfileController`/`ProfileService`/`AdminUserAccountService`/`AdminAchievementService` còn dùng `UserManager` tới Task 4-5. Gỡ ở Task 6.

- [ ] **Step 1: Sửa `Program.cs` — thêm cookie scheme + policy claim**

Thay block `builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();` (Program.cs:65-66) bằng:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
builder.Services.AddScoped<IPasswordHasher<ltwnc.Models.Entities.AppUser>,
    PasswordHasher<ltwnc.Models.Entities.AppUser>>();
builder.Services.AddScoped<IAuthService, AuthService>();
```

Giữ nguyên `AddIdentityCookies()` — tức thêm dòng trên mà KHÔNG xóa `AddIdentityCookies()` ở task này. Cụ thể sau khi sửa, đoạn đó là:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddIdentityCookies();
```

Thay policy (Program.cs:68-74):

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAreaPolicy.Name, policy =>
    {
        policy.RequireClaim(AppClaimTypes.IsAdmin, "true");
    });
});
```

Thay toàn bộ block `builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, ...)` (Program.cs:76-141) bằng (lưu ý đổi scheme sang `CookieAuthenticationDefaults.AuthenticationScheme` và viết lại `OnValidatePrincipal`):

```csharp
builder.Services.Configure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme,
    options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            // Kiểm tra mỗi request: user còn tồn tại, security stamp khớp, không bị khóa.
            string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? stamp = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(stamp))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            AppDbContext dbContext =
                context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            TimeProvider timeProvider =
                context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
            ltwnc.Models.Entities.AppUser? user = await dbContext.AppUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == userId);

            DateTimeOffset now = timeProvider.GetUtcNow();
            bool locked = user?.LockoutEnd != null && user.LockoutEnd > now;
            if (user == null || user.SecurityStamp != stamp || locked)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
```

Xóa block `builder.Services.Configure<SecurityStampValidatorOptions>(...)` (Program.cs:143-146). Các using `Microsoft.AspNetCore.Identity`, `Microsoft.AspNetCore.Authentication` giữ nguyên (còn dùng tới Task 6). Cần thêm `using Microsoft.EntityFrameworkCore;` đã có sẵn ở dòng 1.

- [ ] **Step 2: Viết lại `Controllers/AccountController.cs` (toàn bộ)**

```csharp
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ltwnc.Controllers;

public class AccountController : Controller
{
    private static readonly TimeSpan RegisterCookieLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan RememberMeCookieLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionCookieLifetime = TimeSpan.FromDays(1);

    private readonly IAuthService _authService;
    private readonly IAdminAuditService _adminAuditService;

    public AccountController(
        IAuthService authService,
        IAdminAuditService adminAuditService)
    {
        _authService = authService;
        _adminAuditService = adminAuditService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? usernameError = UsernamePolicy.GetValidationError(model.Username);
        if (usernameError != null)
        {
            ModelState.AddModelError(nameof(RegisterViewModel.Username), usernameError);
            return View(model);
        }

        AuthResult result = await _authService.RegisterAsync(
            model.Email.Trim(),
            model.Username.Trim(),
            model.Password);
        if (!result.Succeeded)
        {
            foreach (AuthError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            return View(model);
        }

        AppUser? user = await _authService.FindByEmailAsync(model.Email.Trim());
        if (user != null)
        {
            await _authService.SignInAsync(user, RegisterCookieLifetime);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public Task<IActionResult> Login()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult<IActionResult>(Redirect(GetAuthenticatedLandingPath()));
        }

        return Task.FromResult<IActionResult>(View());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetAuthenticatedLandingPath());
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AppUser? user = await _authService.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        AuthResult result = await _authService.ValidateLoginAsync(user, model.Password);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                AddLockedAccountMessage();
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        TimeSpan lifetime = model.RememberMe ? RememberMeCookieLifetime : SessionCookieLifetime;
        await _authService.SignInAsync(user, lifetime);

        if (user.IsAdmin)
        {
            await RecordAdminSignInAuditAsync(user);
            return Redirect("/Admin");
        }

        return Redirect("/Set");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private string GetAuthenticatedLandingPath() =>
        User.HasClaim(AppClaimTypes.IsAdmin, "true") ? "/Admin" : "/Set";

    // Ghi audit sau khi Admin đăng nhập thành công; không ghi mật khẩu hoặc thông tin nhạy cảm.
    private async Task RecordAdminSignInAuditAsync(AppUser user)
    {
        await _adminAuditService.RecordAsync(new AdminAuditEntry(
            ActorUserId: user.Id,
            ActorDisplay: user.Email,
            Action: AdminAuditActions.AdminAreaSignIn,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "AppUser",
            TargetId: user.Id,
            CorrelationId: HttpContext.TraceIdentifier));
    }

    // Thông báo chung cho tài khoản bị khóa, không lộ lý do nội bộ do Admin nhập.
    private void AddLockedAccountMessage()
    {
        ModelState.AddModelError(
            string.Empty,
            "Tài khoản hiện không thể đăng nhập. Vui lòng liên hệ bộ phận hỗ trợ để được kiểm tra.");
    }
}
```

(Kiểm tra `AdminAuditEntry` là positional record với các tham số như trên — nếu `ActorDisplay`/tên tham số khác thì sửa cho khớp file `Services/Audit/` hiện có; giữ nguyên cách gọi từ code cũ, chỉ đổi `TargetType` thành `"AppUser"` và `ActorDisplay` lấy `user.Email`.)

- [ ] **Step 3: Viết lại seed trong `tests/ltwnc.Tests/Infrastructure/AdminWebApplicationFactory.cs`**

Thay `SeedUserAsync`, `IsLockedOutAsync`, `GetUserIdAsync`, `GetSecurityStampAsync` bằng bản dùng `AppDbContext` trực tiếp (xóa using `Microsoft.AspNetCore.Identity` và method `EnsureSucceeded`):

```csharp
    public async Task SeedUserAsync(
        string userName,
        string email,
        bool isAdmin = false,
        bool twoFactorEnabled = false)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ltwnc.Models.Entities.AppUser>();

        string normalizedEmail = email.ToUpperInvariant();
        ltwnc.Models.Entities.AppUser? user = await dbContext.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail);
        if (user == null)
        {
            user = new ltwnc.Models.Entities.AppUser
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = normalizedEmail
            };
            user.PasswordHash = passwordHasher.HashPassword(user, TestPassword);
            dbContext.AppUsers.Add(user);
        }

        user.IsAdmin = isAdmin;
        await dbContext.SaveChangesAsync();

        // Tham số cũ được giữ để các test hiện có không phải đổi chữ ký helper.
        _ = twoFactorEnabled;
    }
```

```csharp
    public async Task<bool> IsLockedOutAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        return user.LockoutEnd != null && user.LockoutEnd > Clock.GetUtcNow();
    }

    // Lấy mã AppUser theo email để test có thể gọi trang chi tiết Admin/Users.
    public async Task<string> GetUserIdAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        return user.Id;
    }

    // Lấy concurrency stamp hiện tại để test giả lập form hợp lệ hoặc form bị cũ.
    public async Task<string> GetSecurityStampAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        return user.ConcurrencyStamp;
    }

    // Đổi security stamp để mô phỏng thu hồi phiên (cookie cũ phải bị đăng xuất).
    public async Task RotateSecurityStampAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ltwnc.Models.Entities.AppUser> FindUserByEmailAsync(
        AppDbContext dbContext,
        string email)
    {
        string normalizedEmail = email.ToUpperInvariant();
        return await dbContext.AppUsers
            .SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail)
            ?? throw new InvalidOperationException("Không tìm thấy ngườoi dùng thử nghiệm.");
    }
```

Các method `SignInAsync`/`SubmitLoginAsync`/`SubmitFormAsync` giữ nguyên (login form không đổi).

- [ ] **Step 4: Viết lại `tests/ltwnc.Tests/Controllers/AccountControllerTests.cs`**

Xóa toàn bộ nội dung cũ, thay bằng test mock `IAuthService`:

```csharp
using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Audit;
using ltwnc.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IAdminAuditService> _auditService = new();

    private AccountController CreateController()
    {
        return new AccountController(_authService.Object, _auditService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AppUser CreateUser(bool isAdmin = false) => new()
    {
        Email = "a@example.com",
        NormalizedEmail = "A@EXAMPLE.COM",
        UserName = "alice",
        NormalizedUserName = "ALICE",
        IsAdmin = isAdmin
    };

    [Fact]
    public async Task RegisterPost_InvalidModelState_ReturnsView()
    {
        AccountController controller = CreateController();
        controller.ModelState.AddModelError("Email", "required");

        IActionResult result = await controller.Register(new RegisterViewModel());

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task RegisterPost_AuthFailure_MapsErrorsToModelState()
    {
        _authService
            .Setup(service => service.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Failure(new AuthError("DuplicateEmail", "Email đã được sử dụng.")));
        AccountController controller = CreateController();
        var model = new RegisterViewModel { Email = "a@example.com", Username = "alice", Password = "Password1" };

        IActionResult result = await controller.Register(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Contains(view.ViewData.ModelState.Values.SelectMany(v => v.Errors),
            error => error.ErrorMessage == "Email đã được sử dụng.");
    }

    [Fact]
    public async Task RegisterPost_Success_SignsInAndRedirectsHome()
    {
        AppUser user = CreateUser();
        _authService
            .Setup(service => service.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Success());
        _authService
            .Setup(service => service.FindByEmailAsync("a@example.com", default))
            .ReturnsAsync(user);
        AccountController controller = CreateController();
        var model = new RegisterViewModel { Email = "a@example.com", Username = "alice", Password = "Password1" };

        IActionResult result = await controller.Register(model);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        _authService.Verify(service => service.SignInAsync(user, TimeSpan.FromDays(1)), Times.Once);
    }

    [Fact]
    public async Task LoginPost_UnknownEmail_ReturnsGenericError()
    {
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync((AppUser?)null);
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Contains(view.ViewData.ModelState.Values.SelectMany(v => v.Errors),
            error => error.ErrorMessage == "Email hoặc mật khẩu không đúng.");
    }

    [Fact]
    public async Task LoginPost_LockedOut_ShowsLockedMessage()
    {
        AppUser user = CreateUser();
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _authService
            .Setup(service => service.ValidateLoginAsync(user, It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.LockedOut());
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Contains(view.ViewData.ModelState.Values.SelectMany(v => v.Errors),
            error => error.ErrorMessage.Contains("không thể đăng nhập"));
    }

    [Fact]
    public async Task LoginPost_AdminUser_RedirectsAdminAndAudits()
    {
        AppUser user = CreateUser(isAdmin: true);
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _authService
            .Setup(service => service.ValidateLoginAsync(user, It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Success());
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Admin", redirect.Url);
        _auditService.Verify(service => service.RecordAsync(
            It.Is<AdminAuditEntry>(entry => entry.Action == AdminAuditActions.AdminAreaSignIn),
            default), Times.Once);
    }

    [Fact]
    public async Task LoginPost_RegularUser_RedirectsSet()
    {
        AppUser user = CreateUser();
        _authService
            .Setup(service => service.FindByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(user);
        _authService
            .Setup(service => service.ValidateLoginAsync(user, It.IsAny<string>(), default))
            .ReturnsAsync(AuthResult.Success());
        AccountController controller = CreateController();
        var model = new LoginViewModel { Email = "a@example.com", Password = "Password1" };

        IActionResult result = await controller.Login(model);

        RedirectResult redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Set", redirect.Url);
    }

    [Fact]
    public async Task Logout_SignsOutAndRedirectsHome()
    {
        AccountController controller = CreateController();

        IActionResult result = await controller.Logout();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        _authService.Verify(service => service.SignOutAsync(), Times.Once);
    }
}
```

Nếu `RegisterViewModel`/`LoginViewModel` có thêm property bắt buộc khác, khởi tạo cho đủ. Nếu `IAdminAuditService.RecordAsync` có chữ ký khác (ví dụ tham số CancellationToken không default), điều chỉnh `Verify` cho khớp interface hiện có trong `Services/Audit/`.

- [ ] **Step 5: Build + chạy test liên quan**

Run: `dotnet build && dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~AccountControllerTests|FullyQualifiedName~AdminLoginFlowTests|FullyQualifiedName~AuthServiceTests"`
Expected: Build succeeded; tất cả PASS. (Các test Admin khác có thể FAIL ở trạng thái trung gian này vì service admin chưa rewire — xử lý ở Task 5; KHÔNG chạy full suite ở step này.)

- [ ] **Step 6: Commit**

```bash
git add Program.cs Controllers/AccountController.cs tests/ltwnc.Tests/Infrastructure/AdminWebApplicationFactory.cs tests/ltwnc.Tests/Controllers/AccountControllerTests.cs
git commit -m "feat: cookie auth tu quan trong Program.cs, rewire AccountController sang IAuthService"
```

---

### Task 4: Rewire ProfileController + ProfileService

**Files:**
- Modify: `Controllers/ProfileController.cs`
- Modify: `Services/Profiles/ProfileService.cs`
- Test: `tests/ltwnc.Tests/Controllers/ProfileControllerTests.cs`, `tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs` (viết lại)

**Interfaces:**
- Consumes: `IAuthService` (Task 2), `AppUser` (Task 1).
- Produces: `ProfileService` mới chỉ phụ thuộc `AppDbContext`, `IAuthService`, `TimeProvider`.

Điểm đơn giản hóa lớn: `AppUser` và `UserProfile` nằm trong CÙNG `AppDbContext`, nên đổi username + profile thành 1 `SaveChangesAsync` duy nhất — bỏ hoàn toàn vụ transaction + compensation `SetUserNameAsync` của code cũ.

- [ ] **Step 1: Sửa `ProfileController` — thay `SignInManager` bằng `IAuthService`**

- Xóa field/ctor param `SignInManager<IdentityUser> _signInManager`, thêm `IAuthService _authService` (từ `ltwnc.Services.Auth` — đã có using). Xóa using `Microsoft.AspNetCore.Identity`.
- Hai đoạn refresh (trong `Edit` POST và `ChangePassword`, hiện ở ProfileController.cs:114-118 và 157-161) thay bằng:

```csharp
        AppUser? user = await _authService.FindByIdAsync(_currentUser.UserId);
        if (user != null)
        {
            await _authService.RefreshSignInAsync(user);
        }
```

- Thêm using `ltwnc.Models.Entities;`.

- [ ] **Step 2: Sửa `ProfileService` — bỏ `UserManager`**

Thay ctor: bỏ `UserManager<IdentityUser> _userManager`, thêm `IAuthService _authService` (using `ltwnc.Services.Auth`, `ltwnc.Models.Entities`; xóa using `Microsoft.AspNetCore.Identity` và `Microsoft.EntityFrameworkCore.Storage` nếu không còn dùng).

Các thay đổi theo method:

1. `GetPublicProfileAsync`: thay `IdentityUser? user = await _userManager.FindByNameAsync(username.Trim());` bằng:

```csharp
        string normalizedUserName = username.Trim().ToUpperInvariant();
        AppUser? user = await _db.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.NormalizedUserName == normalizedUserName, cancellationToken);
```

Mọi `user.UserName ?? username` / `user.Id` phía dưới giữ nguyên (AppUser.UserName non-nullable, `?? username` có thể bỏ hoặc giữ — compiler sẽ cảnh báo, bỏ `?? username`).

2. `FindUserAsync`:

```csharp
    private async Task<AppUser> FindUserAsync(string userId)
    {
        return await _db.AppUsers.SingleOrDefaultAsync(item => item.Id == userId)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
    }
```

3. `UpdateProfileAsync` — viết lại thân method (giữ nguyên chữ ký). Logic: validate username → check 30 ngày → check trùng → mutate cả user + profile → 1 SaveChanges:

```csharp
    public async Task<ProfileOperationResult> UpdateProfileAsync(
        string userId,
        ProfileEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        string username = model.Username?.Trim() ?? string.Empty;
        string? usernameError = UsernamePolicy.GetValidationError(username);
        if (usernameError != null)
        {
            return ProfileOperationResult.Failure(new ProfileFieldError(
                nameof(ProfileEditViewModel.Username),
                usernameError));
        }

        AppUser user = await FindUserAsync(userId);
        UserProfile profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        if (!string.Equals(user.UserName, username, StringComparison.Ordinal))
        {
            if (profile.LastUsernameChangedAt.HasValue &&
                now - profile.LastUsernameChangedAt.Value < TimeSpan.FromDays(30))
            {
                return ProfileOperationResult.Failure(new ProfileFieldError(
                    nameof(ProfileEditViewModel.Username),
                    "Bạn chỉ có thể đổi tên đăng nhập sau mỗi 30 ngày."));
            }

            string normalizedUserName = username.ToUpperInvariant();
            bool duplicated = await _db.AppUsers.AnyAsync(
                item => item.NormalizedUserName == normalizedUserName && item.Id != userId,
                cancellationToken);
            if (duplicated)
            {
                return ProfileOperationResult.Failure(new ProfileFieldError(
                    nameof(ProfileEditViewModel.Username),
                    "Tên đăng nhập đã được sử dụng."));
            }

            user.UserName = username;
            user.NormalizedUserName = normalizedUserName;
            // Đổi username phải đá cookie cũ (claim Name + stamp không còn khớp sau refresh).
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            profile.LastUsernameChangedAt = now;
        }

        profile.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        profile.IsPublic = model.IsPublic;
        profile.ShowStats = model.ShowStats;
        profile.ShowBadges = model.ShowBadges;
        profile.ShowActivity = model.ShowActivity;
        profile.ShowPublicSets = model.ShowPublicSets;
        profile.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return ProfileOperationResult.Success();
    }
```

Lưu ý: vì username đổi → security stamp đổi → cookie hiện tại bị reject ở request sau. Controller đã gọi `RefreshSignInAsync` ngay sau `UpdateProfileAsync` (Step 1) nên phiên hiện tại được cấp lại claims mới trong cùng response — hành vi tương đương code cũ.

4. `ChangePasswordAsync`:

```csharp
    public async Task<ProfileOperationResult> ChangePasswordAsync(
        string userId,
        ChangePasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        AppUser user = await FindUserAsync(userId);
        AuthResult result = await _authService.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);
        if (result.Succeeded)
        {
            return ProfileOperationResult.Success();
        }

        ProfileFieldError[] errors = result.Errors
            .Select(error => new ProfileFieldError(
                error.Code == "PasswordMismatch"
                    ? nameof(ChangePasswordViewModel.CurrentPassword)
                    : nameof(ChangePasswordViewModel.NewPassword),
                error.Message))
            .ToArray();
        return ProfileOperationResult.Failure(errors);
    }
```

5. `ToEditModel(IdentityUser user, ...)` → đổi tham số thành `AppUser user`; bỏ `?? string.Empty` nếu compiler báo thừa (AppUser.UserName/Email non-nullable).

6. Xóa 2 method private `Failure(string field, IdentityResult result)` và `MapIdentityError(IdentityError error)`.

- [ ] **Step 3: Viết lại `ProfileControllerTests` và `ProfileServiceTests`**

`ProfileControllerTests`: controller giờ nhận `(IProfileService, ICurrentUser, IAuthService, IAvatarService)` — mock cả 4 bằng Moq. Giữ các kịch bản hiện có (Edit/ChangePassword/Avatar/Public), thay mọi setup `SignInManager`/`UserManager` bằng `_authService.Setup(s => s.FindByIdAsync(...))` / `RefreshSignInAsync`. Viết lại theo mẫu `AccountControllerTests` ở Task 3 Step 4 (cùng style Moq + DefaultHttpContext).

`ProfileServiceTests`: `ProfileService` giờ nhận `(AppDbContext, IAuthService, TimeProvider)`. Thay helper `MockUserManager` bằng seed `AppUser` trực tiếp vào InMemory context:

```csharp
    private static AppUser SeedUser(AppDbContext db, string userName = "alice")
    {
        var user = new AppUser
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName.ToUpperInvariant()}@EXAMPLE.COM"
        };
        db.AppUsers.Add(user);
        db.SaveChanges();
        return user;
    }
```

và `Mock<IAuthService>` cho `ChangePasswordAsync`. Giữ nguyên các kịch bản test hiện có (public profile, edit model, update profile, đổi username 30 ngày, trùng username, đổi mật khẩu sai hiện tại...), chỉ đổi cách dựng fixture. Mọi assert về hành vi giữ nguyên — đây là refactor, không đổi logic test.

- [ ] **Step 4: Build + chạy test**

Run: `dotnet build && dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~ProfileControllerTests|FullyQualifiedName~ProfileServiceTests|FullyQualifiedName~ProfileRouteTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Controllers/ProfileController.cs Services/Profiles/ProfileService.cs tests/ltwnc.Tests/Controllers/ProfileControllerTests.cs tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs
git commit -m "refactor: ProfileController/ProfileService dung IAuthService thay UserManager"
```

---

### Task 5: Rewire AdminUserAccountService + AdminAchievementService + models/views

**Files:**
- Modify: `Services/AdminUsers/AdminUserAccountService.cs`
- Modify: `Services/AdminUsers/AdminUserAccountModels.cs`
- Modify: `Services/AdminAchievements/AdminAchievementService.cs`
- Modify: `Areas/Admin/Views/Users/Details.cshtml`
- Modify: `Views/Shared/_Layout.cshtml:58`
- Test: chạy lại integration tests admin (không đổi)

**Interfaces:**
- Consumes: `AppUser`, `AppDbContext.AppUsers`, `IAuthService.RotateSecurityStampAsync`.
- Produces: `AdminUserAccountRow`/`AdminUserAccountDetails` KHÔNG còn `EmailConfirmed`/`LockoutEnabled`.

- [ ] **Step 1: Sửa `AdminUserAccountModels.cs`**

- `AdminUserAccountRow`: xóa field `bool EmailConfirmed` (còn: Id, UserName, Email, IsAdmin, IsLocked, CreatedAtUtc, LockoutEnd).
- `AdminUserAccountDetails`: xóa `bool EmailConfirmed` và `bool LockoutEnabled` (còn: Id, UserName, Email, IsAdmin, IsLocked, AccessFailedCount, ConcurrencyStamp, CreatedAtUtc, UpdatedAtUtc, LockoutEnd).

- [ ] **Step 2: Rewire `AdminUserAccountService`**

- Xóa using `Microsoft.AspNetCore.Identity`; thêm using `ltwnc.Services.Auth` (đã có) — bỏ `_userManager` và `_configuration` khỏi ctor/fields (xóa cả method `IsBootstrapAdmin`).
- Mọi `IdentityUser` → `AppUser`; mọi `_context.Users` → `_context.AppUsers`.
- `_userManager.FindByIdAsync(id)` → `await _context.AppUsers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)` (dùng overload có CancellationToken khi method có sẵn).
- `_userManager.IsInRoleAsync(user, AdminRoleBootstrapper.AdminRole)` → `user.IsAdmin`.
- `BuildAdminUserIdQuery()` — xóa hẳn; `ApplyStatus` nhánh `"admin"` → `users.Where(user => user.IsAdmin)`; `BuildUserRows` bỏ tham số `adminUserIds`, projection dùng `user.IsAdmin` (và bỏ `user.EmailConfirmed` khỏi `AdminUserAccountRow`).
- `GetDetailsAsync`: bỏ `EmailConfirmed`/`LockoutEnabled` khỏi record init; `IsAdmin = user.IsAdmin`.
- `LockAsync` — thay 3 lệnh Identity (`SetLockoutEnabledAsync`/`SetLockoutEndDateAsync`/`UpdateSecurityStampAsync` + `EnsureIdentitySucceeded`) bằng:

```csharp
        // Khóa tài khoản và đổi stamp để cookie cũ bị vô hiệu ở request kế tiếp.
        user.LockoutEnd = PermanentLockoutEnd;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
```

- `UnlockAsync` — thay `SetLockoutEndDateAsync(user, null)` bằng:

```csharp
        // Chỉ xóa lockout, không chạm vào tiến độ học, thành tích hay nội dung của ngườoi dùng.
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
```

- `RevokeSessionsAsync` — thay `UpdateSecurityStampAsync` bằng:

```csharp
        // Thu hồi phiên độc lập với trạng thái khóa tài khoản.
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
```

- `CountActiveAdminsAsync`:

```csharp
    private async Task<int> CountActiveAdminsAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return await _context.AppUsers.CountAsync(
            user => user.IsAdmin && (user.LockoutEnd == null || user.LockoutEnd <= now));
    }
```

- `GetLockDenialReasonAsync`: xóa nhánh `IsBootstrapAdmin` (và tham chiếu `AdminBootstrap:UserId`). Giữ nhánh tự-khóa và nhánh admin-cuối-cùng.
- `BuildAuditEntry`/`RecordAuditAsync`: `TargetType: "IdentityUser"` → `"AppUser"`.
- Xóa method `EnsureIdentitySucceeded`.
- Lưu ý: `LockAsync` hiện dùng transaction Serializable + `_context.SaveChangesAsync` cuối — giữ nguyên cấu trúc transaction; entity `user` giờ tracked bởi `_context` nên mutation được SaveChanges ghi cùng audit.

- [ ] **Step 3: Rewire `AdminAchievementService`**

- Xóa `_userManager` khỏi ctor/fields + using `Microsoft.AspNetCore.Identity`.
- Mọi `IdentityUser` → `AppUser`; mọi `_context.Users` → `_context.AppUsers`.
- `_userManager.FindByIdAsync(command.TargetUserId)` → `await _context.AppUsers.SingleOrDefaultAsync(item => item.Id == command.TargetUserId, cancellationToken)`.
- `TargetType: "IdentityUser"` → `"AppUser"` (có 1 chỗ ~dòng 461).

- [ ] **Step 4: Sửa views**

`Areas/Admin/Views/Users/Details.cshtml`: xóa 2 dòng hiển thị `EmailConfirmed` (~dòng 36-37: `<dt>...</dt><dd>@BooleanLabel(Model.EmailConfirmed)</dd>`) và `LockoutEnabled` (~dòng 40-41). Xóa cả `<dt>` label tương ứng.

`Views/Shared/_Layout.cshtml:58`: thay

```cshtml
@if (User.IsInRole(ltwnc.Services.Auth.AdminRoleBootstrapper.AdminRole))
```

bằng

```cshtml
@if (User.HasClaim(ltwnc.Services.Auth.AppClaimTypes.IsAdmin, "true"))
```

- [ ] **Step 5: Build + chạy integration tests admin**

Run: `dotnet build && dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~AdminUserAccountTests|FullyQualifiedName~AdminAchievementTests|FullyQualifiedName~AdminLoginFlowTests|FullyQualifiedName~AdminAreaAccessTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Services/AdminUsers/ Services/AdminAchievements/ Areas/Admin/Views/Users/Details.cshtml Views/Shared/_Layout.cshtml
git commit -m "refactor: admin services dung AppUser/IsAdmin thay UserManager va role"
```

---

### Task 6: Gỡ Identity hoàn toàn (DI, package, base class, migration drop AspNet*)

**Files:**
- Modify: `Program.cs`
- Modify: `Data/AppDbContext.cs`
- Modify: `ltwnc.csproj`
- Delete: `Services/Auth/AdminRoleBootstrapper.cs`, `tests/ltwnc.Tests/Services/Auth/AdminRoleBootstrapperTests.cs`
- Create (tự sinh): `Migrations/<timestamp>_DropIdentityTables.cs`

**Interfaces:**
- Consumes: mọi task trước (không còn file app nào dùng `UserManager`/`IdentityUser`).
- Produces: `AppDbContext : DbContext`; `UserProfile.UserId` FK trỏ `AppUsers`.

- [ ] **Step 1: Gỡ Identity khỏi `Program.cs`**

- Xóa toàn bộ block `AddIdentityCore<IdentityUser>(...).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders()` (Program.cs:46-63).
- `.AddCookie().AddIdentityCookies()` → bỏ `.AddIdentityCookies()`, chỉ còn `.AddCookie()`.
- Xóa `builder.Services.AddScoped<AdminRoleBootstrapper>();` (Program.cs:168).
- Xóa block bootstrap admin (Program.cs:298-307, đoạn `string? bootstrapAdminUserId = app.Configuration["AdminBootstrap:UserId"]; ...`).
- Xóa using `Microsoft.AspNetCore.Identity` nếu không còn dùng.

- [ ] **Step 2: Đổi `AppDbContext` sang `DbContext` thường**

```csharp
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

// DbContext chính của ứng dụng — auth tự quản qua bảng AppUsers.
public class AppDbContext : DbContext
```

- Xóa using `Microsoft.AspNetCore.Identity` và `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.
- Xóa block `builder.Entity<IdentityUser>().HasIndex(...EmailIndex...)` (AppUser đã có index riêng từ Task 1).
- Đổi FK UserProfile (hiện `entity.HasOne<IdentityUser>()`):

```csharp
            entity.HasOne<AppUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
```

- [ ] **Step 3: Gỡ package khỏi `ltwnc.csproj`**

Xóa dòng:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.9" />
```

KHÔNG thêm package nào (`PasswordHasher<T>` có sẵn trong shared framework).

- [ ] **Step 4: Xóa AdminRoleBootstrapper**

```bash
git rm Services/Auth/AdminRoleBootstrapper.cs tests/ltwnc.Tests/Services/Auth/AdminRoleBootstrapperTests.cs
```

- [ ] **Step 5: Build + tạo migration drop AspNet***

Run: `dotnet build`
Expected: 0 error. Nếu còn lỗi tham chiếu `IdentityUser`/`UserManager` ở file nào → quay lại task tương ứng, không vá tắt.

Run: `dotnet ef migrations add DropIdentityTables`
Expected: migration chứa `DropTable` cho 7 bảng AspNet* (AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims, AspNetRoles, AspNetUsers) + drop FK `UserProfiles → AspNetUsers` và tạo FK mới `UserProfiles → AppUsers`. Mở file migration kiểm tra bằng mắt trước khi tiếp tục. **Migration này xóa dữ liệu tài khoản — đã chốt với ngườoi dùng.**

- [ ] **Step 6: Áp migration vào DB local**

Run: `dotnet ef database update`
Expected: apply thành công cả `AddAppUsers` và `DropIdentityTables`. (Nếu DB local đang có dữ liệu test cũ và update lỗi do constraint, phương án đã chốt là reset: `dotnet ef database drop` rồi `dotnet ef database update`.)

- [ ] **Step 7: Chạy test**

Run: `dotnet test tests/ltwnc.Tests --filter "FullyQualifiedName~AppDbContextTests|FullyQualifiedName~MigrationMetadataTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Program.cs Data/AppDbContext.cs ltwnc.csproj Migrations/
git commit -m "feat: go hoan toan ASP.NET Identity, drop bang AspNet*"
```

---

### Task 7: Dọn tests còn lại + StaleAuthenticationCookieTests + full suite xanh

**Files:**
- Modify: `tests/ltwnc.Tests/Integration/StaleAuthenticationCookieTests.cs` (viết lại)
- Modify: mọi file test còn tham chiếu Identity (xem danh sách Step 1)
- Modify: `tests/ltwnc.Tests/Data/AppDbContextTests.cs` nếu còn dùng `IdentityUser`

**Interfaces:**
- Consumes: factory helpers mới (Task 3 Step 3): `SeedUserAsync`, `RotateSecurityStampAsync`, `GetUserIdAsync`, `GetSecurityStampAsync`.
- Produces: full test suite xanh.

- [ ] **Step 1: Liệt kê chính xác phần việc còn lại**

Run: `grep -rln "IdentityUser\|UserManager\|SignInManager\|RoleManager\|IUserStore\|IdentityConstants\|AdminRoleBootstrapper" tests/ --include="*.cs"`
Expected hiện tại (ngoài factory đã sửa): `Integration/AdminExportTests.cs`, `Integration/ContentReportTests.cs`, `Integration/AdminGlobalSearchTests.cs`, `Integration/AdminEnglishMissionTests.cs`, `Integration/AdminDashboardKpiTests.cs`, `Integration/AdminDashboardLiveSnapshotTests.cs`, `Integration/AdminContentModerationTests.cs`, `Integration/AdminUserAccountTests.cs`, `Services/AdminDashboard/AdminDashboardKpiServiceTests.cs`, `Services/PublicLibrary/PublicLibraryServiceTests.cs`, `Services/Leaderboard/LeaderboardServiceTests.cs`, `Data/AppDbContextTests.cs`, `Integration/StaleAuthenticationCookieTests.cs`.

- [ ] **Step 2: Port từng file theo 3 mẫu**

Mẫu A — chỉ dùng `IdentityUser` làm kiểu dữ liệu seed/query trực tiếp: đổi `IdentityUser` → `ltwnc.Models.Entities.AppUser`, `_context.Users`/`db.Users` → `db.AppUsers`, bỏ gán `EmailConfirmed`/`LockoutEnabled` nếu có, thêm `NormalizedEmail`/`NormalizedUserName` (uppercase) khi tạo entity.

Mẫu B — dùng `UserManager` để tạo user/lấy Id: thay bằng factory helpers (`SeedUserAsync`, `GetUserIdAsync`) hoặc seed trực tiếp như `ProfileServiceTests` Task 4 Step 3. Hash mật khẩu test bằng `new Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>().HashPassword(user, "Testpass1")` (namespace `Microsoft.AspNetCore.Identity` CHỈ còn dùng cho `PasswordHasher` — không bị xóa trong tests).

Mẫu C — mock `IUserStore<IdentityUser>`: xóa mock, dùng `AdminWebApplicationFactory` + seed thật.

- [ ] **Step 3: Viết lại `StaleAuthenticationCookieTests.cs`**

```csharp
using System.Net;
using ltwnc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ltwnc.Tests.Integration;

public class StaleAuthenticationCookieTests
{
    [Fact]
    public async Task ProfileEdit_DeletedCookieUser_RedirectsToLogin()
    {
        await using var factory = new AdminWebApplicationFactory();
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        // Đăng nhập thật rồi xóa user khỏi DB để mô phỏng cookie của user đã bị xóa.
        await factory.SeedUserAsync("alice", "alice@example.com");
        await AdminWebApplicationFactory.SignInAsync(client, "alice@example.com");
        await factory.DeleteUserAsync("alice@example.com");

        HttpResponseMessage response = await client.GetAsync("/Account/Profile/Edit");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ProfileEdit_RotatedSecurityStamp_RedirectsToLogin()
    {
        await using var factory = new AdminWebApplicationFactory();
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SeedUserAsync("alice", "alice@example.com");
        await AdminWebApplicationFactory.SignInAsync(client, "alice@example.com");
        await factory.RotateSecurityStampAsync("alice@example.com");

        HttpResponseMessage response = await client.GetAsync("/Account/Profile/Edit");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }
}
```

Thêm helper `DeleteUserAsync` vào `AdminWebApplicationFactory` (xóa cả `UserProfiles` trước để tránh FK):

```csharp
    public async Task DeleteUserAsync(string email)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ltwnc.Models.Entities.AppUser user = await FindUserByEmailAsync(dbContext, email);
        dbContext.AppUsers.Remove(user);
        await dbContext.SaveChangesAsync();
    }
```

(FK `UserProfiles → AppUsers` là cascade nên xóa user tự xóa profile.)

- [ ] **Step 4: Chạy FULL suite**

Run: `dotnet test tests/ltwnc.Tests`
Expected: toàn bộ PASS. Nếu test nào FAIL vì còn tham chiếu Identity sót lại → sửa theo Step 2 rồi chạy lại. Không được bỏ qua test FAIL.

- [ ] **Step 5: Commit**

```bash
git add tests/
git commit -m "test: don sach tham chieu Identity, viet lai StaleAuthenticationCookieTests"
```

---

### Task 8: Dọn config/docs + rà soát cuối

**Files:**
- Modify: `appsettings.Development.json` (xóa key `AdminBootstrap` nếu có)
- Modify: `README.md` (thêm hướng dẫn tạo admin thủ công)
- Modify: `Data/AppDbContext.cs` comment header nếu còn chữ "Identity"

- [ ] **Step 1: Rà soát sót**

Run: `grep -rn "IdentityUser\|UserManager\|SignInManager\|RoleManager\|AdminRoleBootstrapper\|AdminBootstrap\|RequireRole\|IsInRole" --include="*.cs" --include="*.cshtml" --include="*.json" . | grep -v "/bin/\|/obj/\|/Migrations/\|PasswordHasher\|docs/superpowers"`
Expected: không còn kết quả nào trong code app/tests. (`Migrations/` cũ và spec/plan được phép còn chữ Identity; `PasswordHasher` là ngoại lệ hợp lệ.) Nếu còn → sửa theo mẫu Task 7 Step 2.

- [ ] **Step 2: Xóa `AdminBootstrap` khỏi `appsettings.Development.json`**

Đọc file, xóa section/key `AdminBootstrap` nếu tồn tại. File này đang có thay đổi uncommitted từ trước — CHỈ xóa key `AdminBootstrap`, không đụng phần khác, và commit riêng.

- [ ] **Step 3: Cập nhật README — cách tạo admin sau reset**

Thêm vào `README.md` (mục setup/hướng dẫn chạy):

```markdown
### Tạo tài khoản Admin

Auth tự quản không có cơ chế bootstrap. Sau khi đăng ký tài khoản qua UI, cấp quyền admin bằng SQL:

```sql
UPDATE AppUsers SET IsAdmin = 1 WHERE NormalizedEmail = 'EMAIL@EXAMPLE.COM';
```

Khu vực `/Admin` yêu cầu claim `IsAdmin` — đăng nhập lại sau khi cấp quyền để cookie mới có claim.
```

- [ ] **Step 4: Verify cuối**

Run: `dotnet build && dotnet test tests/ltwnc.Tests`
Expected: Build succeeded, toàn bộ test PASS.

Smoke thủ công (khuyến nghị): `dotnet run`, đăng ký tài khoản mới → vào `/Set`; SQL set `IsAdmin=1` → đăng nhập lại → vào `/Admin`; admin khóa 1 user → user đó bị đăng xuất ở request kế và không login được (thông báo khóa).

- [ ] **Step 5: Commit**

```bash
git add appsettings.Development.json README.md Data/AppDbContext.cs
git commit -m "chore: xoa config AdminBootstrap, them huong dan tao admin vao README"
```

Sau Task 8: báo ngườoi dùng review toàn bộ branch; merge về `master` CHỈ khi ngườoi dùng xác nhận.
