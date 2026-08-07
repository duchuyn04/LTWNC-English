# Neutral Auth UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the approved neutral copy and button hierarchy to the login and registration pages without changing authentication behavior.

**Architecture:** Keep the existing Razor views and shared auth layout. Add one source-level view contract test, update only visible copy and existing class hooks, then add the missing Google button and divider styles in `auth.css`. No new component, script, package, or authentication code is needed.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor, CSS, xUnit

## Global Constraints

- Follow `docs/superpowers/specs/2026-08-07-neutral-auth-ui-design.md` and the approved browser prototype.
- Preserve all authentication actions, form methods, Tag Helpers, validation attributes, autocomplete values, and antiforgery behavior.
- Keep the current image, auth shell, green and cream palette, and responsive breakpoints.
- Do not add a dependency or a separate Google icon asset; render the Google logo as inline SVG in both auth views.
- Preserve existing uncommitted work, especially the `site.css` import in `_AuthLayout.cshtml` and token aliases at the start of `auth.css`.
- Stage only this feature's hunks from `_AuthLayout.cshtml` and `auth.css`.

---

### Task 1: Neutral auth copy

**Files:**
- Create: `tests/ltwnc.Tests/Views/AuthViewTests.cs`
- Modify: `Views/Account/Login.cshtml:15-78`
- Modify: `Views/Account/Register.cshtml:13-91`
- Modify: `Views/Shared/_AuthLayout.cshtml:48-56`

**Interfaces:**
- Consumes: Existing Razor models, controller actions, `auth-*` class names, and `_AuthLayout`.
- Produces: Approved Vietnamese copy and the existing `auth-google-submit` and `auth-divider` hooks for Task 2.

- [ ] **Step 1: Write the failing view contract test**

Create `tests/ltwnc.Tests/Views/AuthViewTests.cs`:

```csharp
namespace ltwnc.Tests.Views;

public sealed class AuthViewTests
{
    [Fact]
    public void AuthViewsUseNeutralCopy()
    {
        string login = Read("Views/Account/Login.cshtml");
        string register = Read("Views/Account/Register.cshtml");
        string layout = Read("Views/Shared/_AuthLayout.cshtml");

        Assert.Contains("<p class=\"auth-eyebrow\">Tài khoản</p>", login);
        Assert.Contains("<h1 id=\"login-title\" class=\"auth-title\">Đăng nhập</h1>", login);
        Assert.Contains("Nhập thông tin tài khoản để tiếp tục.", login);
        Assert.Contains("Đăng nhập bằng Google", login);
        Assert.Contains("class=\"auth-google-icon\"", login);
        Assert.Contains("<svg class=\"auth-google-icon\"", login);
        Assert.Contains("Hoặc đăng nhập bằng tài khoản", login);
        Assert.Contains("Duy trì đăng nhập trên thiết bị này", login);
        Assert.Contains("<button type=\"submit\" class=\"auth-submit\">Đăng nhập</button>", login);
        Assert.DoesNotContain("Đăng nhập <span aria-hidden=\"true\">→</span>", login);
        Assert.Contains("Chưa có tài khoản? <a href=\"/Account/Register\">Đăng ký</a>", login);
        Assert.DoesNotContain("Sẵn sàng học tiếp?", login);
        Assert.DoesNotContain("auth-progress", login);

        Assert.Contains("<p class=\"auth-eyebrow\">Tài khoản mới</p>", register);
        Assert.Contains("<h1 id=\"register-title\" class=\"auth-title\">Tạo tài khoản</h1>", register);
        Assert.Contains("Điền thông tin bên dưới. Mã xác thực sẽ được gửi đến email của bạn.", register);
        Assert.Contains("class=\"auth-google-icon\"", register);
        Assert.Contains("<svg class=\"auth-google-icon\"", register);
        Assert.Contains("Hoặc đăng ký bằng email", register);
        Assert.DoesNotContain("Tạo góc học tập.", register);
        Assert.DoesNotContain("auth-progress", register);

        Assert.Contains("<span>Từ vựng hôm nay</span>", layout);
        Assert.Contains("<span>Xem nghĩa</span>", layout);
        Assert.Contains("Lưu bộ thẻ và theo dõi tiến độ học tập.", layout);
        Assert.DoesNotContain("Word of the day", layout);
        Assert.DoesNotContain("Tap to reveal", layout);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthViewTests.AuthViewsUseNeutralCopy"
```

Expected: FAIL because `Views/Account/Login.cshtml` does not contain `<p class="auth-eyebrow">Tài khoản</p>`.

- [ ] **Step 3: Replace the login copy without changing form behavior**

In `Views/Account/Login.cshtml`, remove the `auth-progress` element and use:

```cshtml
<p class="auth-eyebrow">Tài khoản</p>
<h1 id="login-title" class="auth-title">Đăng nhập</h1>
<p class="auth-intro">Nhập thông tin tài khoản để tiếp tục.</p>
```

Change the Google action and divider to:

```cshtml
<a class="auth-submit auth-google-submit" href="@Url.Action("GoogleLogin", "Account")">
    Đăng nhập bằng Google
</a>
<div class="auth-divider"><span>Hoặc đăng nhập bằng tài khoản</span></div>
```

Keep the existing checkbox and links, changing only their visible copy:

```cshtml
<span>Duy trì đăng nhập trên thiết bị này</span>
```

```cshtml
<button type="submit" class="auth-submit">Đăng nhập</button>
```

```cshtml
<p class="auth-alt">Chưa có tài khoản? <a href="/Account/Register">Đăng ký</a></p>
```

- [ ] **Step 4: Replace the registration copy without changing form behavior**

In `Views/Account/Register.cshtml`, remove the `auth-progress` element and use:

```cshtml
<p class="auth-eyebrow">Tài khoản mới</p>
<h1 id="register-title" class="auth-title">Tạo tài khoản</h1>
<p class="auth-intro">Điền thông tin bên dưới. Mã xác thực sẽ được gửi đến email của bạn.</p>
```

Keep the Google button text as `Đăng ký bằng Google` and change the divider to:

```cshtml
<div class="auth-divider"><span>Hoặc đăng ký bằng email</span></div>
```

Do not change the `Gửi mã xác thực` submit action or any field attributes.

- [ ] **Step 5: Neutralize the shared illustration copy**

In `Views/Shared/_AuthLayout.cshtml`, replace only the text inside the illustration:

```cshtml
<div class="auth-word-card__meta"><span>Từ vựng hôm nay</span><span>01 / 12</span></div>
<strong>curiosity</strong>
<em>/ˌkjʊəriˈɒsəti/</em>
<p>sự tò mò, ham hiểu biết</p>
<div class="auth-word-card__foot"><span>Xem nghĩa</span><span>↻</span></div>
```

```cshtml
<p class="auth-studio__caption">Lưu bộ thẻ và theo dõi tiến độ học tập.</p>
```

Preserve the existing `site.css` link and all image markup.

- [ ] **Step 6: Run the focused test and verify GREEN**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthViewTests.AuthViewsUseNeutralCopy"
```

Expected: PASS, one test passed.

- [ ] **Step 7: Commit only Task 1**

```bash
git add tests/ltwnc.Tests/Views/AuthViewTests.cs Views/Account/Login.cshtml Views/Account/Register.cshtml
git add -p Views/Shared/_AuthLayout.cshtml
# Accept only the illustration copy hunk. Reject the pre-existing site.css import hunk.
git diff --cached --check
git diff --cached --name-only
git commit -m "style(auth): use neutral account copy"
```

Expected staged paths: the new test, both account views, and `_AuthLayout.cshtml` with only the copy hunk.

### Task 2: Button hierarchy and divider styles

**Files:**
- Modify: `tests/ltwnc.Tests/Views/AuthViewTests.cs`
- Modify: `wwwroot/css/auth.css:88-140,176-210`

**Interfaces:**
- Consumes: `auth-submit`, `auth-google-submit`, and `auth-divider` hooks from the Razor views.
- Produces: Dark primary buttons, an outlined Google button, a visible separator, and no unused progress styles.

- [ ] **Step 1: Add the failing CSS contract test**

Add this method to `AuthViewTests` before the private helpers:

```csharp
[Fact]
public void AuthStylesSeparatePrimaryAndGoogleActions()
{
    string css = Read("wwwroot/css/auth.css").Replace("\r\n", "\n");

    Assert.DoesNotContain(".auth-progress", css);
    Assert.Contains("""
.auth-google-submit {
    border-color: var(--auth-line);
    color: var(--auth-ink);
    background: #fff;
    text-decoration: none;
}
""", css);
    Assert.Contains("""
.auth-google-submit:hover {
    border-color: var(--auth-ink);
    color: var(--auth-ink);
    background: var(--sage-soft);
}
""", css);
    Assert.Contains(".auth-divider::before, .auth-divider::after", css);
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthViewTests.AuthStylesSeparatePrimaryAndGoogleActions"
```

Expected: FAIL because `auth.css` still contains `.auth-progress` and has no `.auth-google-submit` rule.

- [ ] **Step 3: Remove unused progress styles**

Delete these rules from `wwwroot/css/auth.css`:

```css
.auth-progress { display: flex; gap: 6px; margin-bottom: 22px; }
.auth-progress span { width: 25px; height: 3px; background: #d8dbd0; }
.auth-progress span:first-child { width: 48px; background: var(--auth-brass); }
```

Also delete `.auth-progress` declarations from the `max-height: 780px` and `max-width: 640px` media queries. No view uses this class after Task 1.

- [ ] **Step 4: Add the approved button and divider styles**

Keep the existing `.auth-submit` dimensions and add `text-decoration` and transitions inside that rule:

```css
    text-decoration: none;
    transition: color 120ms ease, border-color 120ms ease, background 120ms ease;
```

Replace the one-line primary hover rule and add the new rules immediately after it:

```css
.auth-submit:hover { color: #fff; background: #354130; }

.auth-google-submit {
    border-color: var(--auth-line);
    color: var(--auth-ink);
    background: #fff;
    text-decoration: none;
}

.auth-google-submit:hover {
    border-color: var(--auth-ink);
    color: var(--auth-ink);
    background: var(--sage-soft);
}

.auth-divider {
    display: flex;
    align-items: center;
    gap: 12px;
    margin: 14px 0;
    color: var(--auth-muted);
    font-size: 11px;
    white-space: nowrap;
}

.auth-divider::before, .auth-divider::after {
    height: 1px;
    flex: 1;
    content: "";
    background: var(--auth-line);
}
```

Do not change the token aliases at the start of `auth.css`.

- [ ] **Step 5: Run focused and full automated verification**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthViewTests"
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore
dotnet build ltwnc.csproj --no-restore
git diff --check
```

Expected: both auth tests pass, the full test project reports zero failures, the build exits 0, and `git diff --check` reports no whitespace errors.

- [ ] **Step 6: Verify the approved desktop and mobile presentation**

Start the local app in a separate terminal:

```bash
dotnet run --project ltwnc.csproj --no-build --urls http://127.0.0.1:5057
```

Use Playwright to inspect both routes at `1440x900` and `390x844`:

```text
http://127.0.0.1:5057/Account/Login
http://127.0.0.1:5057/Account/Register
```

For each page, verify:

- The approved neutral title, description, and shared illustration copy are visible.
- There is no progress bar.
- The local submit button has a dark green background and white text.
- The Google button has a white background, gray border, and dark green text.
- The divider has lines on both sides of its label.
- Keyboard focus is visible on inputs, buttons, and links.
- At `390x844`, `document.documentElement.scrollWidth <= window.innerWidth` is true.
- Password toggles, forgot-password link, Login/Register links, and Google link retain their existing targets.

Stop the local app after the checks. Store any screenshots under `.tmp/`, which is ignored by Git.

- [ ] **Step 7: Commit only Task 2**

```bash
git add tests/ltwnc.Tests/Views/AuthViewTests.cs
git add -p wwwroot/css/auth.css
# Accept only progress removal and button/divider style hunks. Reject pre-existing token alias changes.
git diff --cached --check
git diff --cached --name-only
git commit -m "style(auth): clarify button hierarchy"
```

Expected staged paths: `AuthViewTests.cs` and `auth.css`, with no unrelated files or token-alias hunk.
