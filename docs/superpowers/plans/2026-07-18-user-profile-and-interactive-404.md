# User Profile and Interactive 404 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng User Profile công khai/riêng tư có timeline, chỉnh sửa tài khoản và avatar crop; đồng thời xây dựng trang 404 tương tác concept Wrong Turn dưới dạng prototype độc lập và MVC page.

**Architecture:** Profile dùng bảng `UserProfiles` liên kết 1-1 với `IdentityUser`, `ProfileService` tổng hợp dữ liệu học và áp privacy, `AvatarService` quản lý file ảnh. Interactive 404 là module độc lập gồm prototype HTML/CSS/JS và bản Razor dùng status-code re-execution; hai module chỉ dùng chung visual language và accessibility standards.

**Tech Stack:** ASP.NET Core MVC net10.0, ASP.NET Core Identity, EF Core/SQL Server 10.0.9, ImageSharp, Razor, HTML/CSS/JavaScript thuần, xUnit, Moq, EF Core SQLite/InMemory, `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** `docs/superpowers/specs/2026-07-18-user-profile-and-interactive-404-design.md`

## Global Constraints

- Target framework `net10.0`; EF Core và Identity giữ version `10.0.9`.
- Không dùng roles; không gọi `AddIdentity<,>` hoặc `AddRoles<>`.
- Business entities tiếp tục dùng `UserId` kiểu `string`.
- Email không bao giờ xuất hiện trên public profile.
- Profile mới mặc định `IsPublic = true`; `ShowStats`, `ShowBadges`, `ShowActivity`, `ShowPublicSets` mặc định `false`.
- Username chỉ đổi khi lần đổi gần nhất cách ít nhất 30 ngày.
- Mọi timestamp mới và phép tính streak dùng UTC qua `TimeProvider`.
- Avatar nhận JPG, PNG, WebP tối đa 5 MB; server phải decode ảnh thật, không tin extension hoặc `ContentType`.
- Mọi lỗi user-facing bằng tiếng Việt.
- 404 giữ đúng HTTP status `404`; exception `500` tiếp tục dùng `/Home/Error`.
- 404 không dùng chuỗi ký hiệu `-->`, `<--` hoặc lạm dụng mũi tên trong UI.
- Tôn trọng `prefers-reduced-motion`; không thêm animation/UI framework mới.
- Không sửa hoặc stage thay đổi Dictation, auth restore, migration `AddAuth`, hoặc file ngoài danh sách từng task trừ khi task nói rõ.
- Trước khi tạo migration mới, xác nhận `RestoreIdentityAuth` build được và migration chain hiện tại không có pending model changes ngoài `UserProfile`.

---

### Task 1: UserProfile Schema and Identity Registration Hook

**Files:**
- Create: `Models/Entities/UserProfile.cs`
- Modify: `Data/AppDbContext.cs`
- Modify: `Controllers/AccountController.cs`
- Create: `Migrations/<timestamp>_AddUserProfiles.cs`
- Create: `Migrations/<timestamp>_AddUserProfiles.Designer.cs`
- Modify: `Migrations/AppDbContextModelSnapshot.cs`
- Modify: `tests/ltwnc.Tests/Data/AppDbContextTests.cs`
- Modify: `tests/ltwnc.Tests/Controllers/AccountControllerTests.cs`

**Interfaces:**
- Produces: `UserProfile` entity keyed by `UserId`.
- Produces: `DbSet<UserProfile> AppDbContext.UserProfiles`.
- Consumes: Identity registration flow in `AccountController.Register`.

- [ ] **Step 1: Add failing model metadata tests**

Add these tests to `tests/ltwnc.Tests/Data/AppDbContextTests.cs`:

```csharp
[Fact]
public void UserProfile_UsesUserIdAsPrimaryKeyAndIdentityForeignKey()
{
    using AppDbContext db = CreateContext();
    IEntityType entity = db.Model.FindEntityType(typeof(UserProfile))!;

    Assert.Equal(nameof(UserProfile.UserId), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
    IForeignKey foreignKey = Assert.Single(entity.GetForeignKeys());
    Assert.Equal(typeof(IdentityUser), foreignKey.PrincipalEntityType.ClrType);
    Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
}

[Fact]
public void UserProfile_DefaultPrivacyValues_ArePublicBasicOnly()
{
    var profile = new UserProfile { UserId = "user-1" };

    Assert.True(profile.IsPublic);
    Assert.False(profile.ShowStats);
    Assert.False(profile.ShowBadges);
    Assert.False(profile.ShowActivity);
    Assert.False(profile.ShowPublicSets);
}
```

Add required usings for `Microsoft.AspNetCore.Identity`, `Microsoft.EntityFrameworkCore.Metadata`, and `ltwnc.Models.Entities`.

- [ ] **Step 2: Run model tests to verify RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AppDbContextTests.UserProfile" --nologo
```

Expected: build fails because `UserProfile` and `AppDbContext.UserProfiles` do not exist.

- [ ] **Step 3: Add the UserProfile entity and EF mapping**

Create `Models/Entities/UserProfile.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class UserProfile
{
    [Key]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; set; }

    public string? AvatarPath { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool ShowStats { get; set; }
    public bool ShowBadges { get; set; }
    public bool ShowActivity { get; set; }
    public bool ShowPublicSets { get; set; }
    public DateTime? LastUsernameChangedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

Add to `AppDbContext`:

```csharp
public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
```

Add after Identity model configuration in `OnModelCreating`:

```csharp
builder.Entity<UserProfile>(entity =>
{
    entity.HasKey(profile => profile.UserId);
    entity.Property(profile => profile.UserId).HasMaxLength(450);
    entity.Property(profile => profile.Bio).HasMaxLength(500);
    entity.HasOne<IdentityUser>()
        .WithOne()
        .HasForeignKey<UserProfile>(profile => profile.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 4: Create a profile during successful registration**

Inject `AppDbContext` and `TimeProvider` into `AccountController`. After `CreateAsync` succeeds and before sign-in, add:

```csharp
_db.UserProfiles.Add(new UserProfile
{
    UserId = user.Id,
    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
    UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
});
await _db.SaveChangesAsync();
```

Update existing `AccountControllerTests` constructor setup to use an InMemory `AppDbContext` and `TimeProvider.System`. Add:

```csharp
[Fact]
public async Task Register_Success_CreatesDefaultProfile()
{
    var userManager = MockUserManager();
    userManager.Setup(manager => manager.CreateAsync(It.IsAny<IdentityUser>(), "Pass1234"))
        .ReturnsAsync(IdentityResult.Success);
    var signInManager = MockSignInManager(userManager);
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;
    using var db = new AppDbContext(options);
    var controller = new AccountController(
        userManager.Object,
        signInManager.Object,
        db,
        TimeProvider.System);

    await controller.Register(ValidRegister());

    UserProfile profile = Assert.Single(db.UserProfiles);
    Assert.True(profile.IsPublic);
    Assert.False(profile.ShowStats);
    Assert.False(profile.ShowBadges);
    Assert.False(profile.ShowActivity);
    Assert.False(profile.ShowPublicSets);
}
```

- [ ] **Step 5: Run focused tests to verify GREEN**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AppDbContextTests|FullyQualifiedName~AccountControllerTests.Register" --nologo
```

Expected: all selected tests pass.

- [ ] **Step 6: Generate and inspect the migration**

Run:

```powershell
dotnet ef migrations add AddUserProfiles --project ltwnc.csproj
```

Inspect the generated `Up` method. It must create only `UserProfiles`, with primary key `UserId`, FK to `AspNetUsers.Id`, `Bio` max 500, and the privacy defaults from Global Constraints. It must not create role tables or recreate `Users`.

- [ ] **Step 7: Verify migration and commit**

Run:

```powershell
dotnet build ltwnc.csproj --nologo
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AppDbContextTests|FullyQualifiedName~AccountControllerTests.Register" --nologo
```

Expected: build succeeds and focused tests pass.

Commit:

```powershell
git add Models/Entities/UserProfile.cs Data/AppDbContext.cs Controllers/AccountController.cs Migrations/ tests/ltwnc.Tests/Data/AppDbContextTests.cs tests/ltwnc.Tests/Controllers/AccountControllerTests.cs
git commit -m "feat(profile): add user profile schema and registration defaults"
```

---

### Task 2: Profile Query Service, Privacy, Statistics, Streak, and Timeline

**Files:**
- Create: `Models/ViewModels/Profile/PublicProfileViewModel.cs`
- Create: `Models/ViewModels/Profile/ProfileStatisticsViewModel.cs`
- Create: `Models/ViewModels/Profile/ProfileTimelineItemViewModel.cs`
- Create: `Models/ViewModels/Profile/ProfileBadgeViewModel.cs`
- Create: `Models/ViewModels/Profile/ProfilePublicSetViewModel.cs`
- Create: `Services/Profiles/IProfileService.cs`
- Create: `Services/Profiles/ProfileService.cs`
- Modify: `Program.cs`
- Create: `tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs`

**Interfaces:**
- Produces: `Task<PublicProfileViewModel?> GetPublicProfileAsync(string username, string? viewerUserId, CancellationToken cancellationToken = default)`.
- Produces: privacy-filtered profile sections and UTC streak calculation.
- Consumes: `AppDbContext`, `UserManager<IdentityUser>`, `TimeProvider`.

- [ ] **Step 1: Define view-model contracts**

Create the following records/classes:

```csharp
namespace ltwnc.Models.ViewModels.Profile;

public sealed class ProfileStatisticsViewModel
{
    public int OwnedSetCount { get; init; }
    public int PublicSetCount { get; init; }
    public int TotalFlashcardCount { get; init; }
    public int LearnedFlashcardCount { get; init; }
    public int CompletedSessionCount { get; init; }
    public int UnlockedBadgeCount { get; init; }
    public int CurrentStreak { get; init; }
}

public sealed class ProfileTimelineItemViewModel
{
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public string? Detail { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ProfileBadgeViewModel
{
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public DateTime UnlockedAt { get; init; }
}

public sealed class ProfilePublicSetViewModel
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public int CardCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

Create `PublicProfileViewModel` with `Username`, `Bio`, `AvatarPath`, `AvatarInitial`, `IsOwner`, `IsPrivate`, four `Show*` flags, nullable `Statistics`, and read-only lists for `Timeline`, `Badges`, `PublicSets`. Do not add an email property.

- [ ] **Step 2: Write failing query-service tests**

In `ProfileServiceTests.cs`, use SQLite InMemory with real EF queries and mocked `UserManager`. Add tests covering:

```csharp
[Fact]
public async Task GetPublicProfile_PrivateProfile_ReturnsPrivateShellForNonOwner()
{
    PublicProfileViewModel? result = await service.GetPublicProfileAsync("private-user", "viewer");

    Assert.NotNull(result);
    Assert.True(result.IsPrivate);
    Assert.Null(result.Statistics);
    Assert.Empty(result.Timeline);
    Assert.Empty(result.Badges);
    Assert.Empty(result.PublicSets);
}

[Fact]
public async Task GetPublicProfile_HiddenSections_AreNotReturned()
{
    PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", "viewer"))!;

    Assert.Null(result.Statistics);
    Assert.Empty(result.Timeline);
    Assert.Empty(result.Badges);
    Assert.Empty(result.PublicSets);
}

[Fact]
public async Task GetPublicProfile_VisibleSections_ReturnCorrectCountsAndNewestTwentyEvents()
{
    PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", null))!;

    Assert.NotNull(result.Statistics);
    Assert.Equal(20, result.Timeline.Count);
    Assert.True(result.Timeline.SequenceEqual(result.Timeline.OrderByDescending(item => item.Timestamp)));
}

[Theory]
[InlineData("2026-07-18", "2026-07-17", 2)]
[InlineData("2026-07-17", "2026-07-16", 2)]
[InlineData("2026-07-18", "2026-07-16", 1)]
public async Task GetPublicProfile_StreakUsesUtcActiveDays(
    string firstDay,
    string secondDay,
    int expected)
{
    // Seed events on the supplied UTC dates and set TimeProvider to 2026-07-18T12:00:00Z.
    PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", null))!;
    Assert.Equal(expected, result.Statistics!.CurrentStreak);
}
```

Seed separate events from `StudySessions.CompletedAt`, `UserAchievements.UnlockedAt`, and public `FlashcardSets.CreatedAt` so each activity source participates in the tests.

- [ ] **Step 3: Run query tests to verify RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileServiceTests.GetPublicProfile" --nologo
```

Expected: build fails because `IProfileService`, `ProfileService`, and profile view models do not exist.

- [ ] **Step 4: Implement query contracts and service**

Create `IProfileService.cs`:

```csharp
public interface IProfileService
{
    Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default);
}
```

For `GetPublicProfileAsync`:

1. Normalize and find username through `UserManager`.
2. Return `null` when no user exists.
3. Load `UserProfile`; construct defaults without persisting for public reads when absent.
4. Determine owner with ordinal comparison of user IDs.
5. Return private shell immediately for non-owner when `IsPublic` is false.
6. Query each section only when owner or its `Show*` flag is true.
7. Merge timeline sources, sort descending, and take 20.
8. Calculate streak from distinct UTC dates. Start from today if active; otherwise start from yesterday; stop at first gap.

Register:

```csharp
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProfileService, ProfileService>();
```

- [ ] **Step 5: Run query tests and full service tests**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileServiceTests" --nologo
```

Expected: all profile query tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Models/ViewModels/Profile/ Services/Profiles/ Program.cs tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs
git commit -m "feat(profile): add privacy-aware profile queries and timeline"
```

---

### Task 3: Profile Edit Commands, Username Cooldown, Email, and Password

**Files:**
- Create: `Models/ViewModels/Profile/ProfileEditViewModel.cs`
- Create: `Models/ViewModels/Profile/ChangeEmailViewModel.cs`
- Create: `Models/ViewModels/Profile/ChangePasswordViewModel.cs`
- Modify: `Services/Profiles/IProfileService.cs`
- Modify: `Services/Profiles/ProfileService.cs`
- Create: `Services/Profiles/ProfileOperationResult.cs`
- Modify: `tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs`

**Interfaces:**
- Adds and implements edit/command methods on `IProfileService`.
- Produces field-addressable Vietnamese errors through `ProfileOperationResult.Errors`.

- [ ] **Step 1: Create edit models and result contract**

Use DataAnnotations:

```csharp
public sealed class ProfileEditViewModel
{
    [Required, StringLength(256)]
    public string Username { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }

    public string Email { get; init; } = string.Empty;
    public string? AvatarPath { get; init; }
    public string AvatarInitial { get; init; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool ShowStats { get; set; }
    public bool ShowBadges { get; set; }
    public bool ShowActivity { get; set; }
    public bool ShowPublicSets { get; set; }
}

public sealed class ChangeEmailViewModel
{
    [Required, EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}

public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
```

Define:

```csharp
public sealed record ProfileFieldError(string Field, string Message);

public sealed class ProfileOperationResult
{
    public bool Succeeded { get; init; }
    public IReadOnlyList<ProfileFieldError> Errors { get; init; } = [];

    public static ProfileOperationResult Success() => new() { Succeeded = true };
    public static ProfileOperationResult Failure(params ProfileFieldError[] errors) =>
        new() { Errors = errors };
}
```

Add these signatures to `IProfileService`:

```csharp
Task<ProfileEditViewModel> GetEditModelAsync(
    string userId,
    CancellationToken cancellationToken = default);

Task<ProfileOperationResult> UpdateProfileAsync(
    string userId,
    ProfileEditViewModel model,
    CancellationToken cancellationToken = default);

Task<ProfileOperationResult> ChangeEmailAsync(
    string userId,
    ChangeEmailViewModel model,
    CancellationToken cancellationToken = default);

Task<ProfileOperationResult> ChangePasswordAsync(
    string userId,
    ChangePasswordViewModel model,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Write failing command tests**

Add tests for:

```csharp
[Fact]
public async Task UpdateProfile_UsernameChangedWithinThirtyDays_ReturnsCooldownError()
{
    ProfileOperationResult result = await service.UpdateProfileAsync("user-1", modelWithNewUsername);

    Assert.False(result.Succeeded);
    Assert.Contains(result.Errors, error =>
        error.Field == nameof(ProfileEditViewModel.Username) &&
        error.Message.Contains("30 ngày"));
}

[Fact]
public async Task UpdateProfile_UsernameChangedAfterThirtyDays_UpdatesIdentityAndTimestamp()
{
    ProfileOperationResult result = await service.UpdateProfileAsync("user-1", modelWithNewUsername);

    Assert.True(result.Succeeded);
    userManager.Verify(manager => manager.SetUserNameAsync(user, "new-name"), Times.Once);
    Assert.Equal(now.UtcDateTime, db.UserProfiles.Single().LastUsernameChangedAt);
}

[Fact]
public async Task ChangeEmail_DuplicateEmail_ReturnsVietnameseFieldError()
{
    ProfileOperationResult result = await service.ChangeEmailAsync(
        "user-1",
        new ChangeEmailViewModel { NewEmail = "used@example.com" });

    Assert.False(result.Succeeded);
    Assert.Contains(result.Errors, error => error.Field == nameof(ChangeEmailViewModel.NewEmail));
}

[Fact]
public async Task ChangePassword_WrongCurrentPassword_ReturnsVietnameseFieldError()
{
    ProfileOperationResult result = await service.ChangePasswordAsync(
        "user-1",
        new ChangePasswordViewModel
        {
            CurrentPassword = "Wrong123",
            NewPassword = "NewPass123",
            ConfirmPassword = "NewPass123"
        });

    Assert.False(result.Succeeded);
    Assert.All(result.Errors, error => Assert.DoesNotContain("Incorrect password", error.Message));
}
```

- [ ] **Step 3: Run command tests to verify RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileServiceTests.UpdateProfile|FullyQualifiedName~ProfileServiceTests.ChangeEmail|FullyQualifiedName~ProfileServiceTests.ChangePassword" --nologo
```

Expected: tests fail because command methods are not implemented.

- [ ] **Step 4: Implement profile commands**

`GetEditModelAsync` must create missing profiles with defaults and handle a concurrent insert by catching `DbUpdateException`, clearing the added entity, and reloading the row.

`UpdateProfileAsync` must:

1. Trim username and bio.
2. Reject bio longer than 500 characters.
3. Check 30-day cooldown only when username changes.
4. Call `SetUserNameAsync`; map Identity errors to Vietnamese.
5. Save profile fields and `LastUsernameChangedAt` with current UTC time.
6. Use an EF transaction; on failure, restore the original username through `SetUserNameAsync` before returning or rethrowing infrastructure errors.

`ChangeEmailAsync` calls `SetEmailAsync` after uniqueness check through `FindByEmailAsync`. `ChangePasswordAsync` calls `ChangePasswordAsync`. Map all known Identity error codes to Vietnamese and use `Đã xảy ra lỗi. Vui lòng thử lại.` as the unknown fallback.

- [ ] **Step 5: Run command and full profile tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileServiceTests" --nologo
```

Expected: all profile service tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Models/ViewModels/Profile/ Services/Profiles/ tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs
git commit -m "feat(profile): add account editing and username cooldown"
```

---

### Task 4: Public Profile and Profile Edit MVC

**Files:**
- Create: `Controllers/ProfileController.cs`
- Create: `Views/Profile/Public.cshtml`
- Create: `Views/Profile/Private.cshtml`
- Create: `Views/Profile/Edit.cshtml`
- Modify: `Views/_ViewImports.cshtml`
- Modify: `Views/Shared/_Layout.cshtml`
- Create: `wwwroot/css/profile.css`
- Create: `tests/ltwnc.Tests/Controllers/ProfileControllerTests.cs`
- Create: `tests/ltwnc.Tests/Views/ProfileMarkupTests.cs`

**Interfaces:**
- Consumes: `IProfileService`, `ICurrentUser`, `SignInManager<IdentityUser>`.
- Produces routes from the approved spec.

- [ ] **Step 1: Write failing controller tests**

Create controller tests covering:

```csharp
[Fact]
public async Task Public_UnknownUsername_ReturnsNotFound()
{
    profileService.Setup(service => service.GetPublicProfileAsync("missing", null, default))
        .ReturnsAsync((PublicProfileViewModel?)null);

    IActionResult result = await controller.Public("missing", default);

    Assert.IsType<NotFoundResult>(result);
}

[Fact]
public async Task Public_PrivateProfile_ReturnsPrivateView()
{
    profileService.Setup(service => service.GetPublicProfileAsync("private", null, default))
        .ReturnsAsync(new PublicProfileViewModel { Username = "private", IsPrivate = true });

    ViewResult result = Assert.IsType<ViewResult>(await controller.Public("private", default));
    Assert.Equal("Private", result.ViewName);
}

[Fact]
public async Task EditPost_ServiceErrors_AreAddedToModelState()
{
    profileService.Setup(service => service.UpdateProfileAsync("user-1", It.IsAny<ProfileEditViewModel>(), default))
        .ReturnsAsync(ProfileOperationResult.Failure(
            new ProfileFieldError(nameof(ProfileEditViewModel.Username), "Tên đăng nhập đã được sử dụng.")));

    ViewResult result = Assert.IsType<ViewResult>(await controller.Edit(model, default));

    Assert.False(controller.ModelState.IsValid);
    Assert.Contains(controller.ModelState[nameof(ProfileEditViewModel.Username)]!.Errors,
        error => error.ErrorMessage == "Tên đăng nhập đã được sử dụng.");
}
```

- [ ] **Step 2: Run controller tests to verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileControllerTests" --nologo
```

Expected: build fails because `ProfileController` does not exist.

- [ ] **Step 3: Implement controller routes**

Create `ProfileController` with constructor dependencies `IProfileService`, `ICurrentUser`, and `SignInManager<IdentityUser>`.

Actions:

```csharp
[AllowAnonymous]
[HttpGet("/u/{username}")]
public async Task<IActionResult> Public(string username, CancellationToken cancellationToken)

[Authorize]
[HttpGet("/Account/Profile/Edit")]
public async Task<IActionResult> Edit(CancellationToken cancellationToken)

[Authorize]
[HttpPost("/Account/Profile/Edit")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(ProfileEditViewModel model, CancellationToken cancellationToken)

[Authorize]
[HttpPost("/Account/Profile/ChangeEmail")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel model, CancellationToken cancellationToken)

[Authorize]
[HttpPost("/Account/Profile/ChangePassword")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
```

After successful username, email, or password changes, call `RefreshSignInAsync` for the current Identity user. Use PRG redirects and Vietnamese `TempData["Success"]` messages.

- [ ] **Step 4: Add views and markup tests**

Public view must render:

- Avatar image or avatar initial.
- Username and bio.
- Only sections enabled by the model.
- Timeline semantic list ordered by the service.
- Owner button linking to `/Account/Profile/Edit`.

Private view must contain `Profile đang ở chế độ riêng tư` and a real link to `/`.

Edit view must contain separate forms with anti-forgery tokens for profile, email, password, and avatar. Add a layout dropdown item linking to `/Account/Profile/Edit`.

Create source-level markup tests asserting those contracts and that no public view renders an email field.

- [ ] **Step 5: Add responsive profile styling**

Create `wwwroot/css/profile.css` implementing the approved Timeline community layout. Use CSS variables already defined by the site where available. Include breakpoints below 768 px, visible `:focus-visible`, circular avatar rendering, and no dependency on JavaScript for reading profile content.

- [ ] **Step 6: Run controller/view tests and commit**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileControllerTests|FullyQualifiedName~ProfileMarkupTests" --nologo
```

Expected: all selected tests pass.

```powershell
git add Controllers/ProfileController.cs Views/Profile/ Views/_ViewImports.cshtml Views/Shared/_Layout.cshtml wwwroot/css/profile.css tests/ltwnc.Tests/Controllers/ProfileControllerTests.cs tests/ltwnc.Tests/Views/ProfileMarkupTests.cs
git commit -m "feat(profile): add public timeline and account profile pages"
```

---

### Task 5: Secure Avatar Upload and Circular Crop

**Files:**
- Modify: `ltwnc.csproj`
- Create: `Services/Profiles/IAvatarService.cs`
- Create: `Services/Profiles/AvatarService.cs`
- Create: `Services/Profiles/AvatarUploadResult.cs`
- Modify: `Controllers/ProfileController.cs`
- Modify: `Views/Profile/Edit.cshtml`
- Create: `wwwroot/js/profile-avatar.js`
- Modify: `wwwroot/css/profile.css`
- Modify: `Program.cs`
- Create: `tests/ltwnc.Tests/Services/Profiles/AvatarServiceTests.cs`
- Modify: `tests/ltwnc.Tests/Views/ProfileMarkupTests.cs`

**Interfaces:**
- Produces: `Task<AvatarUploadResult> ReplaceAvatarAsync(string userId, IFormFile file, CancellationToken cancellationToken = default)`.
- Consumes: `AppDbContext`, `IWebHostEnvironment`, ImageSharp.

- [ ] **Step 1: Add ImageSharp and write failing upload tests**

Add this exact package reference to `ltwnc.csproj`:

```xml
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />
```

Tests must create real in-memory 10x10 PNG/JPG/WebP files with ImageSharp and cover:

```csharp
[Fact]
public async Task ReplaceAvatar_ValidCroppedPng_SavesRandomFileAndUpdatesProfile()

[Fact]
public async Task ReplaceAvatar_FileOverFiveMegabytes_ReturnsVietnameseError()

[Fact]
public async Task ReplaceAvatar_SpoofedImageContent_ReturnsVietnameseError()

[Fact]
public async Task ReplaceAvatar_DatabaseFailure_DeletesNewFileAndKeepsOldAvatar()
```

- [ ] **Step 2: Run avatar tests to verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AvatarServiceTests" --nologo
```

Expected: build fails because avatar service types do not exist.

- [ ] **Step 3: Implement AvatarService**

Define:

```csharp
public sealed class AvatarUploadResult
{
    public bool Succeeded { get; init; }
    public string? AvatarPath { get; init; }
    public string? Error { get; init; }
}
```

Implementation rules:

1. Reject null/empty and files over `5 * 1024 * 1024` bytes.
2. Decode with ImageSharp and derive format from decoded data.
3. Allow only JPEG, PNG, WebP.
4. Require square crop output; reject width/height difference instead of silently trusting client crop.
5. Save normalized image with a GUID filename under `wwwroot/uploads/avatars`.
6. Update `UserProfile.AvatarPath` and `UpdatedAt`.
7. Delete the new file on database failure.
8. Delete the old avatar only after database update succeeds.

Register `IAvatarService` as scoped.

- [ ] **Step 4: Add avatar endpoint and crop UI**

Add controller action:

```csharp
[Authorize]
[HttpPost("/Account/Profile/Avatar")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Avatar(IFormFile avatar, CancellationToken cancellationToken)
```

The edit view includes a file input accepting `.jpg,.jpeg,.png,.webp`, a circular crop viewport, zoom range input, preview canvas, hidden cropped-file submission, `Lưu ảnh đại diện`, and a no-JS message explaining that image crop requires JavaScript.

`profile-avatar.js` must:

- Decode selected image with `FileReader`/`Image`.
- Draw into a square canvas using user pan/zoom values.
- Mask the preview as a circle while preserving square output.
- Export with `canvas.toBlob` and submit through `DataTransfer` as the `avatar` file.
- Support pointer drag and keyboard-accessible zoom input.
- Never use mũi tên text as controls.

- [ ] **Step 5: Run avatar and markup tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AvatarServiceTests|FullyQualifiedName~ProfileMarkupTests" --nologo
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add ltwnc.csproj Services/Profiles/AvatarService.cs Services/Profiles/IAvatarService.cs Services/Profiles/AvatarUploadResult.cs Controllers/ProfileController.cs Views/Profile/Edit.cshtml wwwroot/js/profile-avatar.js wwwroot/css/profile.css Program.cs tests/ltwnc.Tests/Services/Profiles/AvatarServiceTests.cs tests/ltwnc.Tests/Views/ProfileMarkupTests.cs
git commit -m "feat(profile): add secure circular avatar crop"
```

---

### Task 6: Standalone Wrong Turn 404 Prototype

**Files:**
- Create: `prototype/404/index.html`
- Create: `prototype/404/404.css`
- Create: `prototype/404/404.js`
- Create: `tests/ltwnc.Tests/Views/NotFoundPrototypeTests.cs`

**Interfaces:**
- Produces standalone reference markup and behavior for Task 7.
- No backend dependencies.

- [ ] **Step 1: Write failing prototype contract tests**

Create tests that read the three files and assert:

```csharp
[Fact]
public void Prototype_ContainsWrongTurnContentAndHomeLink()
{
    string html = File.ReadAllText(PrototypePath("index.html"));

    Assert.Contains("Bạn vừa rẽ nhầm một hướng.", html);
    Assert.Contains("wrong turn", html);
    Assert.Contains("href=\"/\"", html);
    Assert.Contains("Về trang chủ", html);
    Assert.DoesNotContain("-->", html);
    Assert.DoesNotContain("<--", html);
}

[Fact]
public void Prototype_ReferencesLocalCssAndJavascript()
{
    string html = File.ReadAllText(PrototypePath("index.html"));
    Assert.Contains("href=\"404.css\"", html);
    Assert.Contains("src=\"404.js\"", html);
}
```

Add tests asserting `404.js` contains click, `Space`, and `aria-pressed` behavior; `404.css` contains `prefers-reduced-motion` and a mobile media query.

- [ ] **Step 2: Run prototype tests to verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~NotFoundPrototypeTests" --nologo
```

Expected: tests fail because prototype files do not exist.

- [ ] **Step 3: Build the standalone prototype**

Implement the approved Wrong Turn composition:

- Full viewport green/cream landscape.
- Layered hills and signpost drawn with semantic HTML/CSS.
- Status label `404 / wrong turn`.
- Heading `Bạn vừa rẽ nhầm một hướng.`.
- Vocabulary card front with term, part of speech, and definition.
- Back with example and IPA `/rɔːŋ tɜːrn/`.
- Real home anchor labeled `Về trang chủ`.
- Button labeled `Lật thẻ` with `aria-pressed="false"`.

JavaScript toggles a single `is-flipped` class, updates `aria-pressed`, and handles `Space` only when the card/button is focused. CSS animation duration stays at or below 600 ms and disappears under reduced-motion.

- [ ] **Step 4: Run tests and manually open prototype**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~NotFoundPrototypeTests" --nologo
```

Expected: all prototype tests pass.

Open `prototype/404/index.html` in a browser. Verify desktop, 390px mobile width, mouse flip, keyboard flip, and disabled JavaScript fallback.

- [ ] **Step 5: Commit**

```powershell
git add prototype/404/ tests/ltwnc.Tests/Views/NotFoundPrototypeTests.cs
git commit -m "feat(404): add standalone Wrong Turn prototype"
```

---

### Task 7: MVC 404 Integration and HTTP Contract

**Files:**
- Modify: `Controllers/HomeController.cs`
- Create: `Views/Shared/NotFound.cshtml`
- Create: `wwwroot/css/not-found.css`
- Create: `wwwroot/js/not-found.js`
- Modify: `Program.cs`
- Modify: `tests/ltwnc.Tests/ltwnc.Tests.csproj`
- Create: `tests/ltwnc.Tests/Controllers/HomeControllerTests.cs`
- Create: `tests/ltwnc.Tests/Views/NotFoundMarkupTests.cs`
- Create: `tests/ltwnc.Tests/Integration/StatusCodePageTests.cs`

**Interfaces:**
- Produces: `HomeController.NotFound()` and status-code re-execution at `/Home/NotFound`.
- Preserves: `/Home/Error` behavior for exceptions.

- [ ] **Step 1: Write failing controller and markup tests**

```csharp
[Fact]
public void NotFoundAction_ReturnsViewWith404Status()
{
    var controller = CreateController();

    ViewResult result = Assert.IsType<ViewResult>(controller.NotFoundPage());

    Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
    Assert.Equal("NotFound", result.ViewName);
}
```

Markup tests assert `HideLayoutChrome`, `FullWidth`, real home link, `Lật thẻ`, `aria-pressed`, local CSS/JS references, no forbidden arrow strings, and reduced-motion CSS.

- [ ] **Step 2: Run tests to verify RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~HomeControllerTests.NotFound|FullyQualifiedName~NotFoundMarkupTests" --nologo
```

Expected: tests fail because MVC 404 files/action do not exist.

- [ ] **Step 3: Implement HomeController action and middleware**

Add:

```csharp
[AllowAnonymous]
[HttpGet]
public IActionResult NotFoundPage()
{
    Response.StatusCode = StatusCodes.Status404NotFound;
    return View("NotFound");
}
```

Register before static files/routing endpoint execution, after exception handling setup:

```csharp
app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");
```

Do not alter `Error()` or `/Home/Error`.

- [ ] **Step 4: Port prototype to Razor/static assets**

`NotFound.cshtml` must set:

```csharp
@{
    ViewData["Title"] = "Không tìm thấy trang";
    ViewData["HideLayoutChrome"] = true;
}
```

Render the scene inside `@section FullWidth`, add `@section Styles` and `@section Scripts`, and preserve the standalone DOM IDs/classes so `not-found.js` mirrors `prototype/404/404.js` behavior.

- [ ] **Step 5: Add integration host dependency and HTTP tests**

Add `Microsoft.AspNetCore.Mvc.Testing` version `10.0.9` to the test project. Add this declaration after `app.Run()` so `WebApplicationFactory<Program>` can access the top-level entry point:

```csharp
public partial class Program { }
```

In `StatusCodePageTests`, override `AppDbContext` with InMemory or SQLite and assert:

```csharp
[Fact]
public async Task UnknownRoute_ReturnsCustom404With404Status()
{
    HttpResponseMessage response = await client.GetAsync("/khong-ton-tai");
    string html = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Contains("Bạn vừa rẽ nhầm một hướng.", html);
    Assert.Contains("Về trang chủ", html);
}

[Fact]
public async Task LoginRoute_RemainsSuccessful()
{
    HttpResponseMessage response = await client.GetAsync("/Account/Login");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

- [ ] **Step 6: Run focused and full tests**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~HomeControllerTests|FullyQualifiedName~NotFoundMarkupTests|FullyQualifiedName~StatusCodePageTests" --nologo
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --nologo
```

Expected: focused and full suites pass; unknown route is 404 and valid routes are unchanged.

- [ ] **Step 7: Commit**

```powershell
git add Controllers/HomeController.cs Views/Shared/NotFound.cshtml wwwroot/css/not-found.css wwwroot/js/not-found.js Program.cs tests/ltwnc.Tests/ltwnc.Tests.csproj tests/ltwnc.Tests/Controllers/HomeControllerTests.cs tests/ltwnc.Tests/Views/NotFoundMarkupTests.cs tests/ltwnc.Tests/Integration/StatusCodePageTests.cs
git commit -m "feat(404): integrate Wrong Turn status page"
```

---

### Task 8: Final Integration, Browser Verification, and Documentation

**Files:**
- Modify: `README.md`
- Modify only if test findings require it: files created in Tasks 1-7.

**Interfaces:**
- Verifies all Profile and 404 contracts together.

- [ ] **Step 1: Build and run all tests**

```powershell
dotnet build ltwnc.csproj --nologo
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --nologo
```

Expected: build succeeds with no errors and all tests pass.

- [ ] **Step 2: Apply migrations to a disposable development database**

Use the configured dev database only after explicit approval to reset data. Otherwise create an alternate connection string and run:

```powershell
dotnet ef database update --project ltwnc.csproj
```

Expected: `RestoreIdentityAuth`, `AddUserProfiles`, and any existing migrations apply without role-table recreation or pending migrations.

- [ ] **Step 3: Run Profile browser smoke test**

Verify this exact flow:

1. Register a new user and confirm a default `UserProfile` exists.
2. Open `/Account/Profile/Edit`.
3. Update bio and enable each privacy section.
4. Upload a non-square image, crop in the circular frame, save, and refresh.
5. Open `/u/{username}` signed out; verify avatar, stats, badges, timeline, and public sets.
6. Disable `IsPublic`; verify signed-out view shows private message.
7. Change username; verify new URL works and old URL returns 404.
8. Attempt a second username change; verify Vietnamese 30-day error.
9. Change email/password and confirm current session remains authenticated.

- [ ] **Step 4: Run 404 browser smoke test**

Verify desktop and 390x844 mobile viewport:

1. `/khong-ton-tai` returns 404 and renders Wrong Turn scene.
2. Mouse click and `Space` flip the card.
3. `aria-pressed` changes.
4. `Về trang chủ` returns to `/`.
5. Reduced-motion emulation disables motion.
6. JavaScript disabled still leaves readable content and home link.
7. `/Account/Login` remains 200.

- [ ] **Step 5: Update README**

Add concise sections documenting:

- Public profile URL and privacy defaults.
- Avatar formats and 5 MB limit.
- Username 30-day cooldown.
- Custom 404 behavior and standalone prototype path.
- Migration command for `AddUserProfiles`.

- [ ] **Step 6: Final grep and status checks**

```powershell
rg -n "Email" Views/Profile
rg -n -- "-->|<--" Views/Shared/NotFound.cshtml wwwroot/css/not-found.css wwwroot/js/not-found.js prototype/404
git status --short
```

Expected:

- No email rendering in public profile view.
- No forbidden arrow strings in 404 UI files.
- Git status contains only intentional files.

- [ ] **Step 7: Commit documentation**

```powershell
git add README.md
git commit -m "docs: document profile and interactive 404"
```

## Self-Review Notes

- Spec coverage: profile schema, defaults, privacy, timeline, stats, streak, username cooldown, email/password, avatar crop, public/edit UI, standalone 404, MVC 404, accessibility, reduced motion, HTTP status, tests, and docs each map to a task.
- Type consistency: `IProfileService`, `IAvatarService`, profile view models, and result types are introduced before consumption.
- Isolation: Profile and 404 tasks have no data/service dependency; only final verification combines them.
- Migration safety: plan explicitly blocks `AddUserProfiles` generation until Identity restore migration state is stable and excludes unrelated `AddAuth`/Dictation work from staging.
