# Disable Email Change Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Khóa hoàn toàn khả năng đổi email sau khi đăng ký, đồng thời vẫn hiển thị email hiện tại ở chế độ chỉ đọc.

**Architecture:** Xóa email-change capability ở cả ba boundary: view không render form, controller không expose POST route, và profile service không expose API thay đổi email. Một integration test bảo vệ endpoint cũ ở trạng thái `404`, còn source-contract và markup tests bảo vệ việc capability không bị thêm lại ngoài ý muốn.

**Tech Stack:** ASP.NET Core MVC, ASP.NET Core Identity, Razor, xUnit, Moq, `WebApplicationFactory<Program>`.

## Global Constraints

- Không thay đổi schema database và không tạo migration.
- Không thay đổi đăng ký, đăng nhập, đổi mật khẩu, username/profile hoặc thao tác quản trị tài khoản.
- Không chạm vào các thay đổi Library Prototype đang có trong working tree.
- Email hiện tại phải tiếp tục hiển thị trên `/Account/Profile/Edit`, nhưng không nằm trong form hoặc input chỉnh sửa.

---

### Task 1: Remove email-change capability end-to-end

**Files:**
- Create: `tests/ltwnc.Tests/Profiles/ProfileEmailPolicyTests.cs`
- Modify: `tests/ltwnc.Tests/Views/ProfileMarkupTests.cs:17-31`
- Modify: `tests/ltwnc.Tests/Integration/ProfileRouteTests.cs:93-122`
- Modify: `Views/Profile/Edit.cshtml:134-154`
- Modify: `Controllers/ProfileController.cs:122-162`
- Modify: `Services/Profiles/IProfileService.cs:21-24`
- Modify: `Services/Profiles/ProfileService.cs:227-262`
- Modify: `tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs:273-325`
- Delete: `Models/ViewModels/Profile/ChangeEmailViewModel.cs`

**Interfaces:**
- Consumes: `ProfileEditViewModel.Email`, `ProfileController`, `IProfileService`, ASP.NET Core endpoint routing.
- Produces: profile edit markup containing only `@Model.Email`; no `ChangeEmail` action; no `IProfileService.ChangeEmailAsync`; POST `/Account/Profile/ChangeEmail` returns `404 Not Found`.

- [ ] **Step 1: Write failing policy and markup tests**

Create `tests/ltwnc.Tests/Profiles/ProfileEmailPolicyTests.cs`:

```csharp
namespace ltwnc.Tests.Profiles;

public class ProfileEmailPolicyTests
{
    private static string Root => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void ProfileCode_DoesNotExposeEmailChangeCapability()
    {
        string controller = File.ReadAllText(
            Path.Combine(Root, "Controllers", "ProfileController.cs"));
        string serviceContract = File.ReadAllText(
            Path.Combine(Root, "Services", "Profiles", "IProfileService.cs"));
        string service = File.ReadAllText(
            Path.Combine(Root, "Services", "Profiles", "ProfileService.cs"));
        string changeEmailModel = Path.Combine(
            Root, "Models", "ViewModels", "Profile", "ChangeEmailViewModel.cs");

        Assert.DoesNotContain("ChangeEmail", controller);
        Assert.DoesNotContain("ChangeEmailAsync", serviceContract);
        Assert.DoesNotContain("ChangeEmailAsync", service);
        Assert.False(File.Exists(changeEmailModel));
    }
}
```

Replace the edit-view test in `tests/ltwnc.Tests/Views/ProfileMarkupTests.cs` with:

```csharp
[Fact]
public void EditProfileView_ShowsReadonlyEmailAndKeepsOtherForms()
{
    string view = File.ReadAllText(Path.Combine(Root, "Views", "Profile", "Edit.cshtml"));

    Assert.Contains("@Model.Email", view);
    Assert.Contains("Email không thể thay đổi", view);
    Assert.DoesNotContain("ChangeEmail", view);
    Assert.DoesNotContain("NewEmail", view);
    Assert.DoesNotContain("CurrentPasswordEmail", view);
    Assert.Contains("ChangePassword", view);
    Assert.Contains("AntiForgeryToken", view);
    Assert.Contains("multipart/form-data", view);
    Assert.Contains("data-avatar-cropper", view);
    Assert.Contains("profile-avatar.js", view);
    Assert.Contains("TempData[\"Error\"]", view);
    Assert.Contains("asp-validation-summary=\"All\"", view);
    Assert.Contains("tabindex=\"0\"", view);
}
```

- [ ] **Step 2: Write the failing endpoint test**

Add this test to `tests/ltwnc.Tests/Integration/ProfileRouteTests.cs` before `CreateClient`:

```csharp
[Fact]
public async Task RemovedChangeEmailEndpoint_ReturnsNotFound()
{
    (WebApplicationFactory<Program> factory, _) = CreateFactory();
    await using (factory)
    using (HttpClient client = CreateClient(factory))
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewEmail"] = "new@example.com",
            ["ConfirmEmail"] = "new@example.com",
            ["CurrentPassword"] = "Pass1234"
        });

        HttpResponseMessage response = await client.PostAsync(
            "/Account/Profile/ChangeEmail",
            content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [ ] **Step 3: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileEmailPolicyTests|FullyQualifiedName~ProfileMarkupTests|FullyQualifiedName~ProfileRouteTests"
```

Expected: FAIL because the controller, service contract, service implementation, model file, editable form, and POST endpoint still exist.

- [ ] **Step 4: Replace the email form with readonly markup**

Replace `Views/Profile/Edit.cshtml:134-154` with:

```cshtml
<section id="section-email" class="settings-card">
    <h2>Email đăng nhập</h2>
    <p class="settings-card-desc">Email dùng để đăng nhập và không thể thay đổi.</p>
    <div class="settings-field">
        <span class="settings-field-label">Email hiện tại</span>
        <p class="settings-input" aria-readonly="true">@Model.Email</p>
    </div>
</section>
```

- [ ] **Step 5: Remove the server endpoint and service capability**

Delete the complete `ChangeEmail` action from `Controllers/ProfileController.cs`:

```csharp
[Authorize]
[HttpPost("/Account/Profile/ChangeEmail")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ChangeEmail(
    ChangeEmailViewModel model,
    CancellationToken cancellationToken)
{
    // Delete this complete action.
}
```

Delete this signature from `Services/Profiles/IProfileService.cs`:

```csharp
Task<ProfileOperationResult> ChangeEmailAsync(
    string userId,
    ChangeEmailViewModel model,
    CancellationToken cancellationToken = default);
```

Delete the complete `ChangeEmailAsync` method from `Services/Profiles/ProfileService.cs`, delete `Models/ViewModels/Profile/ChangeEmailViewModel.cs`, and delete the two obsolete service tests named:

```csharp
ChangeEmail_DuplicateEmail_ReturnsVietnameseFieldError
ChangeEmail_WrongCurrentPassword_DoesNotChangeLoginEmail
```

- [ ] **Step 6: Run focused tests and verify GREEN**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~ProfileEmailPolicyTests|FullyQualifiedName~ProfileMarkupTests|FullyQualifiedName~ProfileRouteTests|FullyQualifiedName~ProfileServiceTests"
```

Expected: PASS with zero failed tests. The endpoint test must observe `404 Not Found`, and the policy test must find no email-change capability.

- [ ] **Step 7: Verify the complete solution**

Run:

```powershell
dotnet test
dotnet build --no-restore
git diff --check
git status --short
```

Expected: all tests pass, build reports `0 Error(s)`, `git diff --check` prints no errors, and `git status --short` shows only the email-lock files plus the pre-existing Library Prototype changes.

- [ ] **Step 8: Commit only the email-lock implementation**

Run:

```powershell
git add -- Controllers/ProfileController.cs Services/Profiles/IProfileService.cs Services/Profiles/ProfileService.cs Views/Profile/Edit.cshtml tests/ltwnc.Tests/Integration/ProfileRouteTests.cs tests/ltwnc.Tests/Profiles/ProfileEmailPolicyTests.cs tests/ltwnc.Tests/Services/Profiles/ProfileServiceTests.cs tests/ltwnc.Tests/Views/ProfileMarkupTests.cs Models/ViewModels/Profile/ChangeEmailViewModel.cs
git commit -m "fix(profile): disable email changes"
```

Expected: the commit includes only the files listed above; `Program.cs` and Library Prototype files remain unstaged.
