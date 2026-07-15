# Custom Cookie Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gỡ ASP.NET Identity hoàn toàn; thay bằng bảng `Users` (`AppUser`), hash PBKDF2, cookie authentication, và `ICurrentUser` — giữ `UserId` string trên nghiệp vụ và UX đăng ký/đăng nhập/đăng xuất.

**Architecture:** `IAuthService` tạo/xác thực user và gọi cookie sign-in/out. Controllers không còn `UserManager`/`SignInManager`; lấy id qua `ICurrentUser` (claim `NameIdentifier`). `AppDbContext` kế thừa `DbContext` thường. Dev reset database (không migrate AspNetUsers).

**Tech Stack:** ASP.NET Core MVC (.NET 10), Cookie Authentication, EF Core SQL Server, `KeyDerivation.Pbkdf2`, xUnit + EF InMemory, Moq (mock `ICurrentUser` nếu cần).

**Spec:** `docs/superpowers/specs/2026-07-16-custom-cookie-auth-design.md`

---

## Global Constraints

- **Không** giữ package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` sau khi xong.
- **Không** dùng `UserManager`, `SignInManager`, `IdentityUser`, `IdentityDbContext` trong application code.
- **Giữ** `UserId` kiểu `string` trên mọi entity nghiệp vụ.
- **Không** đổi logic flashcard/study/achievement ngoài chỗ lấy user id.
- Password policy: tối thiểu 8 ký tự, có chữ hoa, chữ thường, chữ số (cập nhật ViewModel nếu regex cũ thiếu chữ thường).
- Message login sai: **chung** — `"Email hoặc mật khẩu không đúng."`
- Comment `//` tiếng Việt ngắn cho code auth mới (cùng style project).
- Sau migration: document `dotnet ef database drop --force` + `dotnet ef database update` cho dev.
- Mỗi task kết thúc bằng build (và test khi task chạm test/DI đầy đủ).

---

## File Structure

| File | Việc |
|------|------|
| `Models/Entities/AppUser.cs` | **Tạo** entity user |
| `Services/Auth/IPasswordHasher.cs` | **Tạo** |
| `Services/Auth/Pbkdf2PasswordHasher.cs` | **Tạo** |
| `Services/Auth/AuthResult.cs` | **Tạo** DTO kết quả auth |
| `Services/Auth/IAuthService.cs` | **Tạo** |
| `Services/Auth/AuthService.cs` | **Tạo** |
| `Services/Auth/ICurrentUser.cs` | **Tạo** |
| `Services/Auth/CurrentUser.cs` | **Tạo** |
| `Data/AppDbContext.cs` | **Sửa** → `DbContext` + `Users` |
| `Models/Entities/FlashcardSet.cs` | **Sửa** bỏ nav Identity |
| `Models/Entities/StudySession.cs` | **Sửa** bỏ nav Identity |
| `Models/Entities/UserProgress.cs` | **Sửa** bỏ nav Identity |
| `Models/Entities/UserStudySettings.cs` | **Sửa** bỏ nav Identity |
| `Controllers/AccountController.cs` | **Sửa** dùng `IAuthService` |
| `Controllers/FlashcardSetController.cs` | **Sửa** `ICurrentUser` |
| `Controllers/StudyController.cs` | **Sửa** `ICurrentUser` |
| `Controllers/CardActionsController.cs` | **Sửa** `ICurrentUser` |
| `Controllers/AchievementsController.cs` | **Sửa** `ICurrentUser` |
| `Program.cs` | **Sửa** cookie auth + DI auth |
| `ltwnc.csproj` | **Sửa** gỡ package Identity |
| `Models/ViewModels/Account/RegisterViewModel.cs` | **Sửa** regex password nếu thiếu lowercase |
| `Migrations/*` | **Tạo** migration mới (hoặc quy trình reset) |
| `tests/ltwnc.Tests/Services/Auth/*` | **Tạo** test hasher + AuthService |
| `tests/ltwnc.Tests/Controllers/*` | **Sửa** bỏ mock UserManager |
| `tests/ltwnc.Tests/Services/FlashcardSets/*` | **Sửa** bỏ IdentityUser |
| `tests/ltwnc.Tests/Services/CardActions/*` | **Sửa** bỏ IdentityUser |
| `README.md` | **Sửa** mục auth + dev DB |

---

### Task 1: `AppUser` + password hasher (có unit test)

**Mục đích:** Nền tảng lưu user và hash mật khẩu, không phụ thuộc Identity.

**Files:**
- Create: `Models/Entities/AppUser.cs`
- Create: `Services/Auth/IPasswordHasher.cs`
- Create: `Services/Auth/Pbkdf2PasswordHasher.cs`
- Create: `tests/ltwnc.Tests/Services/Auth/Pbkdf2PasswordHasherTests.cs`

**Interfaces:**
- Produces: `AppUser`, `IPasswordHasher.Hash` / `Verify`

- [ ] **Step 1: Tạo entity `AppUser`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Tài khoản ứng dụng (bảng Users) — thay ASP.NET Identity.
public class AppUser
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // Chuỗi hash versioned do Pbkdf2PasswordHasher tạo
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Tạo `IPasswordHasher`**

```csharp
namespace ltwnc.Services.Auth;

// Hash và verify mật khẩu (PBKDF2), không dùng ASP.NET Identity.
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
```

- [ ] **Step 3: Implement `Pbkdf2PasswordHasher`**

Yêu cầu kỹ thuật:
- Dùng `Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2`
- PRF: HMACSHA256
- Iteration count: **100_000** (ghi rõ constant)
- Salt: 16 bytes random (`RandomNumberGenerator`)
- Subkey: 32 bytes
- Format lưu: `v1.{iterations}.{saltBase64}.{subkeyBase64}`
- `Verify`: parse format; iteration/salt/hash sai format → `false`; so sánh hash bằng `CryptographicOperations.FixedTimeEquals`

```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ltwnc.Services.Auth;

// PBKDF2 versioned: v1.{iterations}.{salt}.{hash}
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        // TODO implement: generate salt, derive key, join format
        throw new NotImplementedException();
    }

    public bool Verify(string password, string passwordHash)
    {
        // TODO implement: parse, re-derive, fixed-time equals
        throw new NotImplementedException();
    }
}
```

(Thay `NotImplementedException` bằng implementation đầy đủ trong cùng step — plan chỉ phác format; implementer viết body hoàn chỉnh.)

- [ ] **Step 4: Viết unit test hasher**

```csharp
using ltwnc.Services.Auth;
using Xunit;

namespace ltwnc.Tests.Services.Auth;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_succeeds_for_same_password()
    {
        string hash = _hasher.Hash("Secret1A");
        Assert.True(_hasher.Verify("Secret1A", hash));
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        string hash = _hasher.Hash("Secret1A");
        Assert.False(_hasher.Verify("Wrong999", hash));
    }

    [Fact]
    public void Verify_fails_for_garbage_hash_string()
    {
        Assert.False(_hasher.Verify("Secret1A", "not-a-valid-hash"));
    }

    [Fact]
    public void Hash_produces_v1_prefixed_payload()
    {
        string hash = _hasher.Hash("Secret1A");
        Assert.StartsWith("v1.", hash);
    }
}
```

- [ ] **Step 5: Chạy test hasher**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~Pbkdf2PasswordHasherTests"
```

**Kỳ vọng:** 4 passed.

- [ ] **Step 6: Commit**

```powershell
git add Models/Entities/AppUser.cs Services/Auth/IPasswordHasher.cs Services/Auth/Pbkdf2PasswordHasher.cs tests/ltwnc.Tests/Services/Auth/Pbkdf2PasswordHasherTests.cs
git commit -m "feat(auth): add AppUser entity and PBKDF2 password hasher"
```

---

### Task 2: `IAuthService`, `ICurrentUser`, `AuthResult`

**Mục đích:** API đăng ký/đăng nhập/đăng xuất + đọc user hiện tại từ claims.

**Files:**
- Create: `Services/Auth/AuthResult.cs`
- Create: `Services/Auth/IAuthService.cs`
- Create: `Services/Auth/AuthService.cs`
- Create: `Services/Auth/ICurrentUser.cs`
- Create: `Services/Auth/CurrentUser.cs`
- Create: `tests/ltwnc.Tests/Services/Auth/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `IPasswordHasher`, `AppDbContext`, `IHttpContextAccessor`
- Produces: `IAuthService`, `ICurrentUser`

- [ ] **Step 1: `AuthResult`**

```csharp
namespace ltwnc.Services.Auth;

// Kết quả Register/Login — map sang ModelState ở controller.
public sealed class AuthResult
{
    public bool Succeeded { get; init; }

    // Danh sách lỗi hiển thị form (không chứa password thô)
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static AuthResult Success() => new() { Succeeded = true };

    public static AuthResult Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };
}
```

- [ ] **Step 2: `IAuthService` + `ICurrentUser`**

```csharp
namespace ltwnc.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string userName, string email, string password);

    Task<AuthResult> LoginAsync(string email, string password, bool rememberMe);

    Task LogoutAsync();
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    // ClaimTypes.NameIdentifier
    string? UserId { get; }

    string? UserName { get; }
}
```

- [ ] **Step 3: `CurrentUser`**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.Auth;

// Đọc claims cookie hiện tại (scoped).
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name);
}
```

- [ ] **Step 4: `AuthService` — logic đầy đủ**

Phụ thuộc: `AppDbContext`, `IPasswordHasher`, `IHttpContextAccessor`.

**Chuẩn hóa email:** `email.Trim().ToLowerInvariant()` khi lưu và khi tìm.

**Validate password (server-side, bổ sung ViewModel):**
- length >= 8
- có `[A-Z]`, `[a-z]`, `\d`

**RegisterAsync:**
1. Validate username/email/password; fail → `AuthResult.Failure(...)`.
2. Nếu email hoặc username đã tồn tại → failure tiếng Việt.
3. `Id = Guid.NewGuid().ToString()`.
4. `PasswordHash = _hasher.Hash(password)`.
5. `Users.Add` + `SaveChangesAsync`.
6. Gọi private `SignInAsync(user, isPersistent: true, expires: 1 day)` (giống post-register cũ).
7. Return Success.

**LoginAsync:**
1. Tìm user theo email đã normalize.
2. Nếu null hoặc `Verify` false → Failure generic `"Email hoặc mật khẩu không đúng."`.
3. `expires = rememberMe ? 30 days : 1 day`; `SignInAsync` persistent true với `ExpiresUtc`.
4. Success.

**SignInAsync (private):** tạo `ClaimsIdentity` scheme `CookieAuthenticationDefaults.AuthenticationScheme` với claims NameIdentifier, Name, Email; `HttpContext.SignInAsync`.

**LogoutAsync:** `HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)`.

**Lưu ý test:** `AuthService` cần `HttpContext` — trong unit test inject `IHttpContextAccessor` với `DefaultHttpContext` + service collection có authentication **hoặc** extract sign-in ra interface `ICookieAuth` để mock.  

**Cách khuyến nghị cho test dễ:** tách interface mỏng:

```csharp
public interface ISignInService
{
    Task SignInAsync(AppUser user, bool rememberMe, TimeSpan cookieLifetime);
    Task SignOutAsync();
}
```

`CookieSignInService` dùng `IHttpContextAccessor` + cookie API.  
`AuthService` phụ thuộc `ISignInService` thay vì gọi cookie trực tiếp — unit test mock `ISignInService`.

Thêm file:
- `Services/Auth/ISignInService.cs`
- `Services/Auth/CookieSignInService.cs`

- [ ] **Step 5: Unit test `AuthService` với InMemory + mock `ISignInService`**

```csharp
// Register_creates_user_and_calls_sign_in
// Register_duplicate_email_fails
// Login_wrong_password_fails_without_sign_in
// Login_success_calls_sign_in
```

Dùng `DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString())`.

- [ ] **Step 6: Chạy test Auth**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~Services.Auth"
```

**Kỳ vọng:** hasher + AuthService tests pass.

- [ ] **Step 7: Commit**

```powershell
git add Services/Auth/ tests/ltwnc.Tests/Services/Auth/
git commit -m "feat(auth): add AuthService, sign-in abstraction, and CurrentUser"
```

---

### Task 3: `AppDbContext` + gỡ Identity navigation trên entities

**Mục đích:** Model EF không còn Identity.

**Files:**
- Modify: `Data/AppDbContext.cs`
- Modify: `Models/Entities/FlashcardSet.cs`
- Modify: `Models/Entities/StudySession.cs`
- Modify: `Models/Entities/UserProgress.cs`
- Modify: `Models/Entities/UserStudySettings.cs`

- [ ] **Step 1: Đổi `AppDbContext`**

Trước: `IdentityDbContext` + `using Microsoft.AspNetCore.Identity.EntityFrameworkCore`.

Sau:

```csharp
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;

namespace ltwnc.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<FlashcardSet> FlashcardSets => Set<FlashcardSet>();
    // ... giữ toàn bộ DbSet hiện có

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // KHÔNG gọi base Identity — chỉ DbContext chuẩn
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.UserName).IsUnique();
        });

        // Giữ nguyên cấu hình FlashcardSets, Flashcards, ... hiện có
        // Xóa mọi quan hệ HasOne IdentityUser nếu có trong file
    }
}
```

Đọc toàn bộ `OnModelCreating` hiện tại: **giữ** indexes/relationships nghiệp vụ; **xóa** block liên quan Identity user relationship nếu EF đã cấu hình.

- [ ] **Step 2: Gỡ nav Identity trên 4 entity**

Với mỗi file: xóa `using Microsoft.AspNetCore.Identity;`, xóa property:

```csharp
[ForeignKey(nameof(UserId))]
public IdentityUser? User { get; set; }
```

Giữ `UserId` string + comment cập nhật: “Id user trong bảng Users (cookie auth)”.

- [ ] **Step 3: Build**

```powershell
dotnet build ltwnc.csproj
```

**Kỳ vọng:** compile được (Program.cs vẫn còn Identity tạm — có thể fail nếu Identity bắt buộc store). Nếu build fail vì Program còn AddIdentity: **chấp nhận** và chuyển ngay Task 4, hoặc comment tạm AddIdentity — tốt nhất làm Task 3+4 liền mạch.

- [ ] **Step 4: Commit**

```powershell
git add Data/AppDbContext.cs Models/Entities/FlashcardSet.cs Models/Entities/StudySession.cs Models/Entities/UserProgress.cs Models/Entities/UserStudySettings.cs
git commit -m "refactor(data): AppDbContext without Identity and drop Identity navigations"
```

---

### Task 4: `Program.cs` + gỡ package Identity

**Mục đích:** Cookie auth + DI auth; không còn Identity.

**Files:**
- Modify: `Program.cs`
- Modify: `ltwnc.csproj`

- [ ] **Step 1: Sửa `ltwnc.csproj`**

Xóa:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.9" />
```

Giữ EF SqlServer + Tools.

- [ ] **Step 2: Viết lại khối auth trong `Program.cs`**

Xóa toàn bộ:

```csharp
using Microsoft.AspNetCore.Identity;
// AddIdentity ... ConfigureApplicationCookie ...
```

Thêm:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using ltwnc.Services.Auth;

// ...

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ISignInService, CookieSignInService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
```

Giữ `UseAuthentication` / `UseAuthorization` như hiện có.

Giữ đăng ký application services domain (FlashcardSets, Study, …).

- [ ] **Step 3: Build**

```powershell
dotnet build
```

**Kỳ vọng:** Có thể fail controllers vẫn inject UserManager — Task 5 sẽ sửa. Nếu muốn build xanh sớm, làm Task 5 ngay sau Task 4 trước commit gộp, hoặc commit WIP. **Khuyến nghị:** hoàn thành Task 5 rồi mới commit Task 4+5 nếu build đỏ giữa chừng.

---

### Task 5: Controllers — `AccountController` + `ICurrentUser` mọi nơi

**Mục đích:** Không còn reference Identity ở tầng web.

**Files:**
- Modify: `Controllers/AccountController.cs`
- Modify: `Controllers/FlashcardSetController.cs`
- Modify: `Controllers/StudyController.cs`
- Modify: `Controllers/CardActionsController.cs`
- Modify: `Controllers/AchievementsController.cs`
- Modify: `Models/ViewModels/Account/RegisterViewModel.cs` (regex lowercase)

- [ ] **Step 1: Cập nhật `RegisterViewModel` password regex**

Thành (có chữ thường):

```csharp
[RegularExpression(
    @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
    ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ hoa, 1 chữ thường và 1 số.")]
```

- [ ] **Step 2: Viết lại `AccountController`**

```csharp
using Microsoft.AspNetCore.Mvc;
using ltwnc.Models.ViewModels.Account;
using ltwnc.Services.Auth;

namespace ltwnc.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AuthResult result = await _authService.RegisterAsync(
            model.Username,
            model.Email,
            model.Password);

        if (result.Succeeded)
        {
            return RedirectToAction("Index", "Home");
        }

        foreach (string error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AuthResult result = await _authService.LoginAsync(
            model.Email,
            model.Password,
            model.RememberMe);

        if (result.Succeeded)
        {
            return Redirect("/Set");
        }

        foreach (string error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }
}
```

- [ ] **Step 3: Pattern thay UserManager trên controller khác**

Constructor:

```csharp
// Trước
UserManager<IdentityUser> userManager

// Sau
ICurrentUser currentUser
```

Field: `private readonly ICurrentUser _currentUser;`

Mỗi chỗ:

```csharp
// Trước
IdentityUser? user = await _userManager.GetUserAsync(User);
if (user == null) { ... }
// dùng user.Id

// Sau
string? userId = _currentUser.UserId;
if (userId == null)
{
    return Challenge(); // hoặc logic null tương đương hiện tại
}
// dùng userId
```

Áp dụng **toàn bộ** `GetUserAsync` trong:
- `FlashcardSetController` (~10 chỗ)
- `StudyController` (~11 chỗ)
- `CardActionsController` (2 chỗ)
- `AchievementsController` (1 chỗ)

Xóa `using Microsoft.AspNetCore.Identity`.

Thêm `using ltwnc.Services.Auth`.

- [ ] **Step 4: Build solution**

```powershell
dotnet build
```

**Kỳ vọng:** 0 error (tests có thể còn IdentityUser — sửa Task 7).

- [ ] **Step 5: Commit**

```powershell
git add Program.cs ltwnc.csproj Controllers/ Models/ViewModels/Account/RegisterViewModel.cs
git commit -m "feat(auth): cookie authentication and controllers on ICurrentUser"
```

(Nếu Task 4 chưa commit riêng, gộp Program.cs + csproj vào commit này.)

---

### Task 6: EF migration + dev database reset

**Mục đích:** Schema DB khớp model mới.

**Files:**
- Create: migration mới dưới `Migrations/`

- [ ] **Step 1: Tạo migration**

```powershell
dotnet ef migrations add ReplaceIdentityWithAppUsers --project ltwnc.csproj
```

Kiểm tra migration:
- Có `CreateTable Users` (hoặc tương đương)
- Drop các bảng AspNet* **hoặc** document rằng dev phải drop database

**Lưu ý:** Nếu migration “Drop AspNet*” từ model snapshot Identity phức tạp/lỗi, chiến lược sạch hơn:

```powershell
# Chỉ khi team đồng ý reset lịch sử migration (dev only)
# Xóa folder Migrations cũ, tạo InitialCreate mới từ model hiện tại
```

Spec cho phép **drop database**. Khuyến nghị thực tế:
1. Thử migration incremental trước.
2. Nếu snapshot Identity quá rối → xóa migrations + `InitialCreate` mới + drop DB.

- [ ] **Step 2: Áp dụng DB (dev machine)**

```powershell
dotnet ef database drop --force --project ltwnc.csproj
dotnet ef database update --project ltwnc.csproj
```

- [ ] **Step 3: Commit migration**

```powershell
git add Migrations/
git commit -m "feat(db): replace Identity tables with Users for custom auth"
```

---

### Task 7: Sửa tests + README + verify cuối

**Mục đích:** Suite xanh; docs đúng.

**Files:**
- Modify: controller tests, FlashcardSet copy tests, DeleteCardsCommandTests
- Create (nếu chưa): Auth tests còn thiếu
- Modify: `README.md`

- [ ] **Step 1: `StudyControllerIndexTests` / `DictationTests`**

Bỏ mock `UserManager` / `IUserStore`.

Cách A — mock `ICurrentUser`:

```csharp
var currentUser = new Mock<ICurrentUser>();
currentUser.Setup(c => c.UserId).Returns(userId);
currentUser.Setup(c => c.IsAuthenticated).Returns(userId != null);
// pass currentUser.Object vào constructor StudyController
```

Cách B — set `HttpContext.User` claims + real `CurrentUser` (cần accessor).  
**Khuyến nghị: Cách A** (đơn giản).

- [ ] **Step 2: Tests tạo `IdentityUser`**

`FlashcardSetCopySqliteTests`, `DeleteCardsCommandTests`, v.v.:

```csharp
// Xóa new IdentityUser { ... }
// Chỉ gán set.UserId = "owner-1"; learnerId = "learner-1";
```

- [ ] **Step 3: Grep sạch Identity**

```powershell
rg "IdentityUser|UserManager|SignInManager|IdentityDbContext|AspNetCore.Identity" -g "*.cs" -g "!Migrations/**"
```

**Kỳ vọng:** không còn match trong app + tests (trừ có thể comment lịch sử — nên xóa hết).

- [ ] **Step 4: README**

- Công nghệ: thay “ASP.NET Identity” bằng “Cookie authentication + custom Users table”.
- Mục dev: sau pull, `database drop` + `update` nếu schema auth đổi.
- Mục tính năng: đăng ký/đăng nhập vẫn liệt kê, không nói Identity.

- [ ] **Step 5: Full test**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
```

**Kỳ vọng:** tất cả pass.

- [ ] **Step 6: Smoke manual (khuyến nghị)**

```powershell
dotnet run --project ltwnc.csproj
```

1. Register user mới (password đủ rule)  
2. Vào `/Set`  
3. Logout  
4. Login lại  
5. Vào `/Achievements` (Authorize)

- [ ] **Step 7: Commit**

```powershell
git add tests/ README.md
git commit -m "test+docs: align suite and README with custom cookie auth"
```

---

## Verification checklist (cuối)

| Tiêu chí | Kiểm |
|----------|------|
| Không package Identity EF | `ltwnc.csproj` |
| Không UserManager/IdentityUser trong code | `rg` |
| Register/Login/Logout cookie | smoke |
| `[Authorize]` redirect login | smoke |
| `UserId` string business | entities không đổi kiểu |
| Tests xanh | `dotnet test` |
| README | auth + drop DB |

---

## Self-review (plan vs spec)

| Spec | Task |
|------|------|
| AppUser / Users | Task 1 |
| PBKDF2 versioned | Task 1 |
| IAuthService Register/Login/Logout | Task 2 |
| ICurrentUser | Task 2 |
| Cookie claims | Task 2 CookieSignInService |
| AppDbContext DbContext | Task 3 |
| Remove Identity navs | Task 3 |
| Program cookie + remove Identity | Task 4 |
| Controllers | Task 5 |
| Migration + DB reset | Task 6 |
| Tests + README | Task 7 |
| Password policy + generic login error | Task 2 + 5 ViewModel |
| No Identity package | Task 4 |

Không TBD blocker. Implementer ưu tiên **code trên disk** nếu signature lệch nhẹ so với snippet plan.
