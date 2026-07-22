# Production Auth Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the basic login/register cards with the approved Learning Studio auth surface: local image studio on the left, accessible form on the right, and no unnecessary desktop scroll.

**Architecture:** Add a dedicated `_AuthLayout.cshtml` that owns the document shell, responsive studio and shared assets while each account view owns only its bound form. Keep all authentication behavior in the existing ViewModels, controller actions and `IAuthService`; JavaScript is progressive enhancement limited to password visibility.

**Tech Stack:** ASP.NET Core MVC/Razor on .NET 10, xUnit markup contract tests, plain CSS, vanilla JavaScript, Pillow for one-time image optimization.

## Global Constraints

- Desktop is studio left/form right using a 58/42 grid; DOM and mobile order keep the form first.
- Use Warm Editorial tokens: cream paper, forest ink, brass accent, Newsreader display and Be Vietnam Pro body.
- Use `100dvh` with `100vh` fallback; 1440 × 900 and 1366 × 768 must not scroll before validation errors.
- At 980 px and below use one column; the studio is 280 px on tablet and 220 px at 640 px and below.
- At 1200 px and above Register Email/Username share a row; below 1200 px all fields are one column.
- Desktop WebP is at least 1600 × 1200 and at most 300 KB; mobile WebP is at least 960 × 480 and at most 140 KB; include JPEG fallback.
- Do not hotlink images, add password reset, add external auth, change ViewModels, change auth POST behavior, or lock submit with JavaScript.
- Keep forms as Razor tag-helper POST forms so ASP.NET Core continues to generate and validate antiforgery tokens automatically.
- ModelState errors, zoom and short/mobile viewports may scroll naturally; never clip validation content.
- Preserve unrelated dirty-worktree changes. Stage only files owned by the current task.

---

## File Structure

- Create `Views/Shared/_AuthLayout.cshtml`: dedicated auth HTML shell, form-first DOM, studio markup and shared asset loading.
- Modify `Views/Account/Login.cshtml`: production login form and validation binding only.
- Modify `Views/Account/Register.cshtml`: production registration form and validation binding only.
- Create `wwwroot/css/auth.css`: scoped Warm Editorial auth layout, controls, validation and responsive/height behavior.
- Create `wwwroot/js/auth.js`: password reveal/hide progressive enhancement.
- Create `wwwroot/images/auth/auth-learning-studio.webp`: optimized desktop image.
- Create `wwwroot/images/auth/auth-learning-studio-mobile.webp`: optimized mobile crop.
- Create `wwwroot/images/auth/auth-learning-studio.jpg`: JPEG fallback.
- Create `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`: view, asset, style, script and prototype-cleanup contracts.
- Modify `Controllers/AccountController.cs`: remove the development-only prototype action after production views pass.
- Delete `Views/Account/AuthPrototype.cshtml`, `Views/Account/Prototypes/`, `wwwroot/css/auth-prototype.css`, and `wwwroot/js/auth-prototype.js` after the production implementation is verified.

Prototype primary source is archived on branch `codex/prototype-auth-layout-c`; Task 0 creates that branch before production code replaces the prototype.

---

### Task 0: Archive the accepted prototype as primary design evidence

**Files:**
- Archive on branch `codex/prototype-auth-layout-c`: `Controllers/AccountController.cs`
- Archive on branch `codex/prototype-auth-layout-c`: `Views/Account/AuthPrototype.cshtml`
- Archive on branch `codex/prototype-auth-layout-c`: `Views/Account/Prototypes/_AuthVariantA.cshtml`
- Archive on branch `codex/prototype-auth-layout-c`: `Views/Account/Prototypes/_AuthVariantB.cshtml`
- Archive on branch `codex/prototype-auth-layout-c`: `Views/Account/Prototypes/_AuthVariantC.cshtml`
- Archive on branch `codex/prototype-auth-layout-c`: `wwwroot/css/auth-prototype.css`
- Archive on branch `codex/prototype-auth-layout-c`: `wwwroot/js/auth-prototype.js`

**Interfaces:**
- Consumes: the accepted prototype files currently present in `C:\it\ltwnc`.
- Produces: immutable branch `codex/prototype-auth-layout-c` referenced by this plan; production tasks do not depend on its runtime code.

- [ ] **Step 1: Create a temporary worktree for the archive branch**

Run from `C:\it\ltwnc`:

```powershell
$prototypeWorktree = 'C:\tmp\ltwnc-auth-prototype'
git worktree add -b codex/prototype-auth-layout-c $prototypeWorktree HEAD
```

Expected: Git creates the branch and checks it out only in `C:\tmp\ltwnc-auth-prototype`; the user's dirty main worktree is not switched.

- [ ] **Step 2: Copy only the prototype primary-source files into the temporary worktree**

```powershell
$prototypeWorktree = 'C:\tmp\ltwnc-auth-prototype'
New-Item -ItemType Directory -Force -Path "$prototypeWorktree\Views\Account\Prototypes" | Out-Null
New-Item -ItemType Directory -Force -Path "$prototypeWorktree\wwwroot\css" | Out-Null
New-Item -ItemType Directory -Force -Path "$prototypeWorktree\wwwroot\js" | Out-Null
Copy-Item -LiteralPath 'C:\it\ltwnc\Controllers\AccountController.cs' -Destination "$prototypeWorktree\Controllers\AccountController.cs"
Copy-Item -LiteralPath 'C:\it\ltwnc\Views\Account\AuthPrototype.cshtml' -Destination "$prototypeWorktree\Views\Account\AuthPrototype.cshtml"
Copy-Item -LiteralPath 'C:\it\ltwnc\Views\Account\Prototypes\_AuthVariantA.cshtml' -Destination "$prototypeWorktree\Views\Account\Prototypes\_AuthVariantA.cshtml"
Copy-Item -LiteralPath 'C:\it\ltwnc\Views\Account\Prototypes\_AuthVariantB.cshtml' -Destination "$prototypeWorktree\Views\Account\Prototypes\_AuthVariantB.cshtml"
Copy-Item -LiteralPath 'C:\it\ltwnc\Views\Account\Prototypes\_AuthVariantC.cshtml' -Destination "$prototypeWorktree\Views\Account\Prototypes\_AuthVariantC.cshtml"
Copy-Item -LiteralPath 'C:\it\ltwnc\wwwroot\css\auth-prototype.css' -Destination "$prototypeWorktree\wwwroot\css\auth-prototype.css"
Copy-Item -LiteralPath 'C:\it\ltwnc\wwwroot\js\auth-prototype.js' -Destination "$prototypeWorktree\wwwroot\js\auth-prototype.js"
```

- [ ] **Step 3: Verify and commit the archive branch**

```powershell
$prototypeWorktree = 'C:\tmp\ltwnc-auth-prototype'
git -C $prototypeWorktree status --short
git -C $prototypeWorktree add -- Controllers/AccountController.cs Views/Account/AuthPrototype.cshtml Views/Account/Prototypes wwwroot/css/auth-prototype.css wwwroot/js/auth-prototype.js
git -C $prototypeWorktree commit -m "prototype: capture auth layout directions"
git show --stat --oneline codex/prototype-auth-layout-c -1
```

Expected: the branch commit contains exactly the seven prototype source paths listed in this task and records the chosen direction in commit history.

- [ ] **Step 4: Remove only the validated temporary worktree**

```powershell
$prototypeWorktree = 'C:\tmp\ltwnc-auth-prototype'
$resolvedPrototypeWorktree = (Resolve-Path -LiteralPath $prototypeWorktree).Path
if (-not $resolvedPrototypeWorktree.StartsWith('C:\tmp\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Prototype worktree is outside C:\tmp.' }
git worktree remove $resolvedPrototypeWorktree
git branch --list codex/prototype-auth-layout-c
```

Expected: the temporary directory is removed by Git and the archive branch still exists.

---

### Task 1: Production auth shell, responsive layout and local image

**Files:**
- Create: `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`
- Create: `Views/Shared/_AuthLayout.cshtml`
- Create: `wwwroot/css/auth.css`
- Create: `wwwroot/js/auth.js`
- Create: `wwwroot/images/auth/auth-learning-studio.webp`
- Create: `wwwroot/images/auth/auth-learning-studio-mobile.webp`
- Create: `wwwroot/images/auth/auth-learning-studio.jpg`

**Interfaces:**
- Consumes: `ViewData["Title"]`, Razor `@RenderBody()`, local jQuery at `~/lib/jquery/dist/jquery.min.js`.
- Produces: `_AuthLayout`, CSS hooks `.auth-page`, `.auth-shell`, `.auth-panel`, `.auth-studio`, and responsive local image assets used by Tasks 2–4.

- [ ] **Step 1: Write the failing shell/asset/style tests**

Create `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`:

```csharp
namespace ltwnc.Tests.Views;

public class AuthLayoutMarkupTests
{
    private static string Root => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath));

    [Fact]
    public void AuthLayout_UsesDedicatedShellAndLocalResponsiveImage()
    {
        string layout = Read("Views/Shared/_AuthLayout.cshtml");

        Assert.Contains("@RenderBody()", layout);
        Assert.Contains("auth-shell", layout);
        Assert.Contains("auth-panel", layout);
        Assert.Contains("auth-studio", layout);
        Assert.Contains("<picture", layout);
        Assert.Contains("auth-learning-studio.webp", layout);
        Assert.Contains("auth-learning-studio-mobile.webp", layout);
        Assert.Contains("auth-learning-studio.jpg", layout);
        Assert.Contains("~/css/auth.css", layout);
        Assert.Contains("~/js/auth.js", layout);
        Assert.DoesNotContain("images.unsplash.com", layout);
        Assert.DoesNotContain("app-nav", layout);
        Assert.DoesNotContain("app-footer", layout);
    }

    [Fact]
    public void AuthLayout_KeepsFormFirstInDomWhileCssPlacesStudioLeft()
    {
        string layout = Read("Views/Shared/_AuthLayout.cshtml");
        string css = Read("wwwroot/css/auth.css");

        int panelIndex = layout.IndexOf("auth-panel", StringComparison.Ordinal);
        int studioIndex = layout.IndexOf("auth-studio", StringComparison.Ordinal);

        Assert.True(panelIndex >= 0 && studioIndex > panelIndex);
        Assert.Contains("grid-template-areas: \"studio panel\"", css);
        Assert.Contains("grid-template-columns: minmax(0, 58fr) minmax(420px, 42fr)", css);
    }

    [Fact]
    public void AuthStyles_EncodeViewportAndResponsiveContracts()
    {
        string css = Read("wwwroot/css/auth.css");

        Assert.Contains("min-height: 100vh", css);
        Assert.Contains("min-height: 100dvh", css);
        Assert.Contains("@media (max-width: 980px)", css);
        Assert.Contains("grid-template-areas: \"panel\" \"studio\"", css);
        Assert.Contains("height: 280px", css);
        Assert.Contains("@media (max-width: 640px)", css);
        Assert.Contains("height: 220px", css);
        Assert.Contains("@media (max-height: 780px) and (min-width: 981px)", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("overflow-y: auto", css);
    }

    [Fact]
    public void AuthImageAssets_AreLocalAndWithinBudgets()
    {
        string desktopWebp = Path.Combine(Root, "wwwroot", "images", "auth", "auth-learning-studio.webp");
        string mobileWebp = Path.Combine(Root, "wwwroot", "images", "auth", "auth-learning-studio-mobile.webp");
        string jpegFallback = Path.Combine(Root, "wwwroot", "images", "auth", "auth-learning-studio.jpg");

        Assert.True(File.Exists(desktopWebp));
        Assert.True(File.Exists(mobileWebp));
        Assert.True(File.Exists(jpegFallback));
        Assert.InRange(new FileInfo(desktopWebp).Length, 1, 300 * 1024);
        Assert.InRange(new FileInfo(mobileWebp).Length, 1, 140 * 1024);
    }
}
```

- [ ] **Step 2: Run the tests and verify the expected failure**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~ltwnc.Tests.Views.AuthLayoutMarkupTests"
```

Expected: FAIL because `Views/Shared/_AuthLayout.cshtml`, `wwwroot/css/auth.css`, and image assets do not exist.

- [ ] **Step 3: Generate and optimize the production image**

Invoke the `imagegen` skill and call image generation with this exact prompt:

```text
Create a photorealistic 4:3 editorial photograph for a premium Vietnamese English-learning web app. A warm quiet study studio with a young adult learner writing vocabulary in a notebook at a wooden desk, books and subtle flashcards nearby, soft natural window light, muted cream and forest-green palette, authentic candid posture, no visible logos, no readable text, no screens facing camera. Keep the upper-right and lower-left regions visually calm so small UI cards can overlay without covering the face or hands. Subject positioned left-of-center, cinematic but natural, restrained contrast, production website hero photography, 1600x1200 composition.
```

Assign the local output path returned by image generation to the PowerShell variable `$generatedAuthImage`, then run the bundled Pillow conversion:

```powershell
$authImageDir = 'C:\it\ltwnc\wwwroot\images\auth'
New-Item -ItemType Directory -Force -Path $authImageDir | Out-Null
$workspacePython = 'C:\Users\juven\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $workspacePython -c "from PIL import Image; import sys, pathlib; src=Image.open(sys.argv[1]).convert('RGB'); out=pathlib.Path(sys.argv[2]); desktop=src.resize((1600,1200), Image.Resampling.LANCZOS); desktop.save(out/'auth-learning-studio.webp','WEBP',quality=80,method=6); desktop.save(out/'auth-learning-studio.jpg','JPEG',quality=82,optimize=True,progressive=True); mobile=src.resize((1200,900), Image.Resampling.LANCZOS).crop((120,210,1080,690)); mobile.save(out/'auth-learning-studio-mobile.webp','WEBP',quality=76,method=6)" $generatedAuthImage $authImageDir
```

If either WebP exceeds its test budget, lower only its Pillow `quality` by 4 and rerun; do not reduce the required dimensions.

- [ ] **Step 4: Create the dedicated auth layout**

Create `Views/Shared/_AuthLayout.cshtml`:

```cshtml
<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] - LTWNC English</title>
    <meta name="description" content="Đăng nhập hoặc đăng ký LTWNC English để học từ vựng bằng flashcard, nghe chép và hội thoại AI." />
    <link rel="icon" href="~/favicon.ico" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700&family=Newsreader:ital,opsz,wght@0,6..72,600;1,6..72,600&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="~/css/auth.css" asp-append-version="true" />
</head>
<body class="auth-page">
    <div class="auth-page__frame">
        <header class="auth-header">
            <a class="auth-brand" href="/" aria-label="LTWNC English - Trang chủ">
                <span class="auth-brand__mark" aria-hidden="true">L</span>
                <span>LTWNC English</span>
            </a>
            <a class="auth-home-link" href="/">Về trang chủ <span aria-hidden="true">↗</span></a>
        </header>

        <div class="auth-shell">
            <main class="auth-panel">
                @RenderBody()
            </main>

            <aside class="auth-studio" aria-hidden="true">
                <picture class="auth-studio__picture">
                    <source media="(max-width: 980px)" srcset="@Url.Content("~/images/auth/auth-learning-studio-mobile.webp")" type="image/webp" />
                    <source srcset="@Url.Content("~/images/auth/auth-learning-studio.webp")" type="image/webp" />
                    <img src="~/images/auth/auth-learning-studio.jpg"
                         width="1600"
                         height="1200"
                         alt=""
                         loading="eager"
                         fetchpriority="high" />
                </picture>
                <div class="auth-studio__veil"></div>
                <article class="auth-word-card">
                    <div class="auth-word-card__meta"><span>Word of the day</span><span>01 / 12</span></div>
                    <strong>curiosity</strong>
                    <em>/ˌkjʊəriˈɒsəti/</em>
                    <p>sự tò mò, ham hiểu biết</p>
                    <div class="auth-word-card__foot"><span>Tap to reveal</span><span>↻</span></div>
                </article>
                <div class="auth-streak"><strong>12</strong><span>ngày học liên tiếp</span></div>
                <p class="auth-studio__caption">Một tài khoản.<br />Mọi tiến bộ đều được ghi lại.</p>
            </aside>
        </div>
    </div>

    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/js/auth.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 5: Create the scoped layout and responsive CSS**

Create `wwwroot/css/auth.css`:

```css
:root {
    --auth-paper: #f7f3e9;
    --auth-surface: #fffdf7;
    --auth-ink: #293226;
    --auth-muted: #596155;
    --auth-line: #c9cfc0;
    --auth-brass: #b7791f;
    --auth-brass-deep: #8f6115;
    --auth-error: #a33b3b;
    --auth-error-bg: #f7e9e5;
    --auth-display: "Newsreader", Georgia, serif;
    --auth-body: "Be Vietnam Pro", "Segoe UI", sans-serif;
}

* { box-sizing: border-box; }
html { min-height: 100%; background: var(--auth-paper); }

body.auth-page {
    min-height: 100vh;
    min-height: 100dvh;
    margin: 0;
    overflow-x: hidden;
    overflow-y: auto;
    color: var(--auth-ink);
    background: var(--auth-paper);
    font-family: var(--auth-body);
    -webkit-font-smoothing: antialiased;
}

.auth-page__frame {
    display: flex;
    width: min(100%, 1536px);
    min-height: 100vh;
    min-height: 100dvh;
    flex-direction: column;
    margin: 0 auto;
    padding: 20px clamp(24px, 3vw, 52px) 24px;
}

.auth-header {
    display: flex;
    flex: 0 0 auto;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 16px;
}

.auth-brand {
    display: inline-flex;
    align-items: center;
    gap: 10px;
    color: var(--auth-ink);
    font-size: 15px;
    font-weight: 700;
    text-decoration: none;
}

.auth-brand__mark {
    display: grid;
    width: 34px;
    height: 34px;
    place-items: center;
    border: 1px solid currentColor;
    border-radius: 50%;
    font-family: var(--auth-display);
    font-size: 20px;
    font-style: italic;
}

.auth-home-link { color: var(--auth-muted); font-size: 12px; font-weight: 600; text-decoration: none; }

.auth-shell {
    display: grid;
    min-height: 0;
    flex: 1 1 auto;
    grid-template-columns: minmax(0, 58fr) minmax(420px, 42fr);
    grid-template-areas: "studio panel";
    overflow: hidden;
    border: 1px solid #d7dacd;
    border-radius: 24px;
    background: var(--auth-surface);
    box-shadow: 0 24px 70px rgba(41, 50, 38, .09);
}

.auth-panel { display: flex; min-width: 0; grid-area: panel; align-items: center; justify-content: center; padding: clamp(30px, 4vw, 60px); }
.auth-form-card { width: min(100%, 480px); }
.auth-progress { display: flex; gap: 6px; margin-bottom: 22px; }
.auth-progress span { width: 25px; height: 3px; background: #d8dbd0; }
.auth-progress span:first-child { width: 48px; background: var(--auth-brass); }
.auth-eyebrow { margin: 0 0 10px; color: var(--auth-brass-deep); font-size: 12px; font-weight: 700; letter-spacing: .14em; text-transform: uppercase; }
.auth-title { margin: 0; color: var(--auth-ink); font-family: var(--auth-display); font-size: clamp(40px, 4vw, 58px); font-weight: 600; letter-spacing: -.035em; line-height: .98; }
.auth-intro { margin: 12px 0 20px; color: var(--auth-muted); font-size: 14px; line-height: 1.65; }
.auth-form { display: grid; gap: 12px; }
.auth-field { display: grid; min-width: 0; gap: 5px; }
.auth-field-grid { display: grid; gap: 12px; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.auth-label { font-size: 12px; font-weight: 700; }

.auth-input {
    width: 100%;
    height: 44px;
    padding: 0 14px;
    border: 1px solid var(--auth-line);
    border-radius: 10px;
    outline: 0;
    color: var(--auth-ink);
    background: var(--auth-surface);
    font: inherit;
    transition: border-color 120ms ease, box-shadow 120ms ease;
}

.auth-input:focus { border-color: var(--auth-brass); box-shadow: 0 0 0 3px rgba(183, 121, 31, .14); }
.auth-password { position: relative; display: block; }
.auth-password .auth-input { padding-right: 66px; }
.auth-password__toggle { position: absolute; top: 50%; right: 9px; min-width: 44px; min-height: 36px; transform: translateY(-50%); border: 0; color: var(--auth-brass-deep); background: transparent; font-size: 11px; font-weight: 700; cursor: pointer; }
.auth-hint { color: #737a6f; font-size: 10px; line-height: 1.45; }
.auth-check { display: flex; align-items: center; gap: 9px; color: var(--auth-muted); font-size: 12px; }
.auth-check input { width: 16px; height: 16px; accent-color: var(--auth-ink); }

.auth-submit {
    display: flex;
    min-height: 46px;
    align-items: center;
    justify-content: center;
    gap: 10px;
    margin-top: 2px;
    border: 1px solid var(--auth-ink);
    border-radius: 10px;
    color: #fff;
    background: var(--auth-ink);
    font: 700 14px var(--auth-body);
    cursor: pointer;
}

.auth-submit:hover { background: #354130; }
.auth-submit:focus-visible, .auth-brand:focus-visible, .auth-home-link:focus-visible, .auth-password__toggle:focus-visible, .auth-alt a:focus-visible { outline: 2px solid var(--auth-brass); outline-offset: 3px; }
.auth-alt { margin: 16px 0 0; color: var(--auth-muted); font-size: 12px; text-align: center; }
.auth-alt a { color: var(--auth-brass-deep); font-weight: 700; }

.auth-validation-summary { margin-bottom: 4px; border-radius: 10px; color: var(--auth-error); background: var(--auth-error-bg); font-size: 12px; }
.auth-validation-summary.validation-summary-errors { padding: 10px 12px; border: 1px solid rgba(163, 59, 59, .25); }
.auth-validation-summary ul { margin: 0; padding-left: 18px; }
.field-validation-error { color: var(--auth-error); font-size: 11px; }
.input-validation-error { border-color: var(--auth-error); box-shadow: 0 0 0 2px rgba(163, 59, 59, .1); }

.auth-studio { position: relative; min-width: 0; min-height: 0; grid-area: studio; overflow: hidden; background: #dfe9d7; }
.auth-studio__picture, .auth-studio__picture img { position: absolute; inset: 0; width: 100%; height: 100%; }
.auth-studio__picture img { object-fit: cover; object-position: center; }
.auth-studio__veil { position: absolute; inset: 0; background: linear-gradient(180deg, rgba(30, 39, 28, .05), rgba(30, 39, 28, .42)); }

.auth-word-card {
    position: absolute;
    top: 9%;
    right: 7%;
    width: min(55%, 360px);
    padding: 24px;
    transform: rotate(2.5deg);
    border: 1px solid rgba(41, 50, 38, .16);
    border-radius: 18px;
    background: rgba(255, 253, 247, .94);
    box-shadow: 12px 16px 0 rgba(41, 50, 38, .18);
}

.auth-word-card__meta, .auth-word-card__foot { display: flex; justify-content: space-between; color: #777e72; font-size: 9px; letter-spacing: .09em; text-transform: uppercase; }
.auth-word-card strong { display: block; margin-top: clamp(22px, 4vh, 40px); font-family: var(--auth-display); font-size: clamp(32px, 4vw, 54px); font-weight: 600; }
.auth-word-card em { color: var(--auth-brass-deep); font-size: 12px; }
.auth-word-card p { margin: 12px 0 clamp(22px, 4vh, 44px); color: var(--auth-muted); font-size: 13px; }
.auth-streak { position: absolute; bottom: 12%; left: 8%; display: grid; padding: 18px 22px; transform: rotate(-3deg); border-radius: 14px; color: #fff; background: var(--auth-ink); box-shadow: 8px 10px 0 rgba(255, 255, 255, .4); }
.auth-streak strong { font-family: var(--auth-display); font-size: 42px; line-height: 1; }
.auth-streak span { margin-top: 4px; color: #cfd8c8; font-size: 9px; text-transform: uppercase; }
.auth-studio__caption { position: absolute; right: 7%; bottom: 8%; margin: 0; color: #fff; font-family: var(--auth-display); font-size: clamp(22px, 3vw, 34px); font-style: italic; line-height: 1.05; text-align: right; text-shadow: 0 2px 20px rgba(0, 0, 0, .45); }

@media (max-height: 780px) and (min-width: 981px) {
    .auth-page__frame { padding-top: 14px; padding-bottom: 14px; }
    .auth-header { margin-bottom: 10px; }
    .auth-panel { padding: 22px 38px; }
    .auth-progress { margin-bottom: 12px; }
    .auth-title { font-size: 40px; }
    .auth-eyebrow { margin-bottom: 7px; }
    .auth-intro { margin: 7px 0 12px; line-height: 1.5; }
    .auth-form { gap: 8px; }
    .auth-input { height: 40px; }
    .auth-submit { min-height: 42px; }
    .auth-alt { margin-top: 10px; }
}

@media (max-width: 1199px) {
    .auth-field-grid { grid-template-columns: 1fr; }
}

@media (max-width: 980px) {
    .auth-page__frame { padding: 18px 20px 28px; }
    .auth-shell { display: grid; min-height: auto; grid-template-columns: 1fr; grid-template-areas: "panel" "studio"; overflow: visible; }
    .auth-panel { min-height: auto; padding: 44px 28px; }
    .auth-studio { height: 280px; }
    .auth-streak, .auth-studio__caption { display: none; }
    .auth-word-card { top: 28px; right: 28px; width: min(52%, 300px); padding: 18px; }
    .auth-word-card strong { margin-top: 18px; font-size: 38px; }
    .auth-word-card p { margin-bottom: 18px; }
}

@media (max-width: 640px) {
    .auth-page__frame { padding: 14px 12px 22px; }
    .auth-home-link { display: none; }
    .auth-shell { border-radius: 16px; box-shadow: none; }
    .auth-panel { padding: 36px 20px; }
    .auth-progress { margin-bottom: 16px; }
    .auth-title { font-size: 42px; }
    .auth-studio { height: 220px; }
    .auth-word-card { top: 22px; right: 22px; width: 72%; }
}

@media (prefers-reduced-motion: reduce) {
    *, *::before, *::after { scroll-behavior: auto !important; transition-duration: .01ms !important; }
}
```

Create the initial `wwwroot/js/auth.js` so the shell never references a missing static asset before Task 4:

```javascript
(() => {})();
```

- [ ] **Step 6: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~ltwnc.Tests.Views.AuthLayoutMarkupTests"
```

Expected: 4 tests PASS.

- [ ] **Step 7: Commit the shell, tests and assets**

```powershell
git add -- Views/Shared/_AuthLayout.cshtml wwwroot/css/auth.css wwwroot/js/auth.js wwwroot/images/auth tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs
git commit -m "feat: add production auth shell"
```

---

### Task 2: Production login form

**Files:**
- Modify: `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`
- Modify: `Views/Account/Login.cshtml`

**Interfaces:**
- Consumes: `_AuthLayout`, `LoginViewModel`, CSS hooks from Task 1, POST `AccountController.Login(LoginViewModel)`.
- Produces: bound login form with IDs `Email` and `Password`, `[data-password-toggle]` for Task 4, remember-me and validation regions.

- [ ] **Step 1: Add the failing login contract test inside `AuthLayoutMarkupTests`**

```csharp
[Fact]
public void LoginView_UsesAuthLayoutAndPreservesBindingValidationAndRememberMe()
{
    string view = Read("Views/Account/Login.cshtml");

    Assert.Contains("Layout = \"_AuthLayout\"", view);
    Assert.Contains("<form asp-action=\"Login\" method=\"post\"", view);
    Assert.Contains("asp-validation-summary=\"ModelOnly\"", view);
    Assert.Contains("asp-for=\"Email\"", view);
    Assert.Contains("asp-validation-for=\"Email\"", view);
    Assert.Contains("asp-for=\"Password\"", view);
    Assert.Contains("asp-validation-for=\"Password\"", view);
    Assert.Contains("asp-for=\"RememberMe\"", view);
    Assert.Contains("data-password-toggle", view);
    Assert.Contains("aria-controls=\"Password\"", view);
    Assert.Contains("href=\"/Account/Register\"", view);
    Assert.DoesNotContain("Quên mật khẩu", view);
}
```

- [ ] **Step 2: Run the login test and verify it fails**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~LoginView_UsesAuthLayout"
```

Expected: FAIL because `Login.cshtml` still uses the default layout/card markup.

- [ ] **Step 3: Replace `Views/Account/Login.cshtml` with the production form**

```cshtml
@model LoginViewModel
@{
    Layout = "_AuthLayout";
    ViewData["Title"] = "Đăng nhập";
}

<section class="auth-form-card" aria-labelledby="login-title">
    <div class="auth-progress" aria-hidden="true"><span></span><span></span><span></span></div>
    <p class="auth-eyebrow">Phiên học của bạn</p>
    <h1 id="login-title" class="auth-title">Sẵn sàng học tiếp?</h1>
    <p class="auth-intro">Đăng nhập để mở lại bộ thẻ và tiếp tục tiến độ của bạn.</p>

    <form asp-action="Login" method="post" class="auth-form">
        <div asp-validation-summary="ModelOnly" class="auth-validation-summary" role="alert"></div>

        <div class="auth-field">
            <label asp-for="Email" class="auth-label"></label>
            <input asp-for="Email"
                   class="auth-input"
                   placeholder="you@example.com"
                   autocomplete="email"
                   aria-describedby="Email-error"
                   autofocus />
            <span asp-validation-for="Email" id="Email-error"></span>
        </div>

        <div class="auth-field">
            <label asp-for="Password" class="auth-label"></label>
            <span class="auth-password">
                <input asp-for="Password"
                       class="auth-input"
                       placeholder="Mật khẩu của bạn"
                       autocomplete="current-password"
                       aria-describedby="Password-error" />
                <button class="auth-password__toggle"
                        type="button"
                        data-password-toggle
                        aria-controls="Password"
                        aria-label="Hiện mật khẩu">Hiện</button>
            </span>
            <span asp-validation-for="Password" id="Password-error"></span>
        </div>

        <label class="auth-check">
            <input asp-for="RememberMe" />
            <span>Ghi nhớ đăng nhập trên thiết bị này</span>
        </label>

        <button type="submit" class="auth-submit">Đăng nhập <span aria-hidden="true">→</span></button>
    </form>

    <p class="auth-alt">Chưa có tài khoản? <a href="/Account/Register">Đăng ký miễn phí</a></p>
</section>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 4: Run the login contract and relevant integration tests**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~LoginView_UsesAuthLayout|FullyQualifiedName~AccountControllerTests|FullyQualifiedName~ProfileRouteTests"
```

Expected: PASS. Existing login action/model behavior remains unchanged.

- [ ] **Step 5: Commit the login form**

```powershell
git add -- Views/Account/Login.cshtml tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs
git commit -m "feat: redesign login screen"
```

---

### Task 3: Production registration form

**Files:**
- Modify: `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`
- Modify: `Views/Account/Register.cshtml`

**Interfaces:**
- Consumes: `_AuthLayout`, `RegisterViewModel`, CSS hooks from Task 1, POST `AccountController.Register(RegisterViewModel)`.
- Produces: registration form with `[data-password-toggle]` for both password fields and `.auth-field-grid` for the 1200 px two-column rule.

- [ ] **Step 1: Add the failing register contract test inside `AuthLayoutMarkupTests`**

```csharp
[Fact]
public void RegisterView_UsesAuthLayoutAndPreservesAllBindingsAndPasswordHint()
{
    string view = Read("Views/Account/Register.cshtml");

    Assert.Contains("Layout = \"_AuthLayout\"", view);
    Assert.Contains("<form asp-action=\"Register\" method=\"post\"", view);
    Assert.Contains("asp-validation-summary=\"ModelOnly\"", view);
    Assert.Contains("class=\"auth-field-grid\"", view);
    Assert.Contains("asp-for=\"Email\"", view);
    Assert.Contains("asp-for=\"Username\"", view);
    Assert.Contains("asp-for=\"Password\"", view);
    Assert.Contains("asp-for=\"ConfirmPassword\"", view);
    Assert.Equal(2, view.Split("data-password-toggle").Length - 1);
    Assert.Contains("Password-hint Password-error", view);
    Assert.Contains("tối thiểu 8 ký tự", view);
    Assert.Contains("href=\"/Account/Login\"", view);
}
```

- [ ] **Step 2: Run the register test and verify it fails**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~RegisterView_UsesAuthLayout"
```

Expected: FAIL because `Register.cshtml` still uses the default layout/card markup.

- [ ] **Step 3: Replace `Views/Account/Register.cshtml` with the production form**

```cshtml
@model RegisterViewModel
@{
    Layout = "_AuthLayout";
    ViewData["Title"] = "Đăng ký";
}

<section class="auth-form-card" aria-labelledby="register-title">
    <div class="auth-progress" aria-hidden="true"><span></span><span></span><span></span></div>
    <p class="auth-eyebrow">Thiết lập hồ sơ học</p>
    <h1 id="register-title" class="auth-title">Tạo góc học tập.</h1>
    <p class="auth-intro">Đăng ký để lưu bộ thẻ và theo dõi tiến độ của bạn.</p>

    <form asp-action="Register" method="post" class="auth-form">
        <div asp-validation-summary="ModelOnly" class="auth-validation-summary" role="alert"></div>

        <div class="auth-field-grid">
            <div class="auth-field">
                <label asp-for="Email" class="auth-label"></label>
                <input asp-for="Email"
                       class="auth-input"
                       placeholder="you@example.com"
                       autocomplete="email"
                       aria-describedby="Email-error"
                       autofocus />
                <span asp-validation-for="Email" id="Email-error"></span>
            </div>

            <div class="auth-field">
                <label asp-for="Username" class="auth-label"></label>
                <input asp-for="Username"
                       class="auth-input"
                       placeholder="minhnguyen"
                       autocomplete="username"
                       aria-describedby="Username-error" />
                <span asp-validation-for="Username" id="Username-error"></span>
            </div>
        </div>

        <div class="auth-field">
            <label asp-for="Password" class="auth-label"></label>
            <span class="auth-password">
                <input asp-for="Password"
                       class="auth-input"
                       placeholder="Có chữ hoa, chữ thường và số"
                       autocomplete="new-password"
                       aria-describedby="Password-hint Password-error" />
                <button class="auth-password__toggle"
                        type="button"
                        data-password-toggle
                        aria-controls="Password"
                        aria-label="Hiện mật khẩu">Hiện</button>
            </span>
            <small id="Password-hint" class="auth-hint">Mật khẩu tối thiểu 8 ký tự, có chữ hoa, chữ thường và số.</small>
            <span asp-validation-for="Password" id="Password-error"></span>
        </div>

        <div class="auth-field">
            <label asp-for="ConfirmPassword" class="auth-label"></label>
            <span class="auth-password">
                <input asp-for="ConfirmPassword"
                       class="auth-input"
                       placeholder="Nhập lại mật khẩu"
                       autocomplete="new-password"
                       aria-describedby="ConfirmPassword-error" />
                <button class="auth-password__toggle"
                        type="button"
                        data-password-toggle
                        aria-controls="ConfirmPassword"
                        aria-label="Hiện mật khẩu xác nhận">Hiện</button>
            </span>
            <span asp-validation-for="ConfirmPassword" id="ConfirmPassword-error"></span>
        </div>

        <button type="submit" class="auth-submit">Tạo tài khoản</button>
    </form>

    <p class="auth-alt">Đã có tài khoản? <a href="/Account/Login">Đăng nhập</a></p>
</section>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 4: Run the register contract and account tests**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~RegisterView_UsesAuthLayout|FullyQualifiedName~AccountControllerTests"
```

Expected: PASS. Registration policy and server validation remain unchanged.

- [ ] **Step 5: Commit the registration form**

```powershell
git add -- Views/Account/Register.cshtml tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs
git commit -m "feat: redesign registration screen"
```

---

### Task 4: Password visibility behavior and accessibility contracts

**Files:**
- Modify: `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`
- Modify: `wwwroot/js/auth.js`

**Interfaces:**
- Consumes: buttons with `[data-password-toggle]` and `aria-controls` from Tasks 2–3.
- Produces: per-field show/hide behavior; updates `type`, visible button copy and `aria-label` without changing input value.

- [ ] **Step 1: Add the failing script/accessibility test inside `AuthLayoutMarkupTests`**

```csharp
[Fact]
public void AuthScript_TogglesControlledPasswordAndUpdatesAccessibleCopy()
{
    string script = Read("wwwroot/js/auth.js");

    Assert.Contains("[data-password-toggle]", script);
    Assert.Contains("getAttribute(\"aria-controls\")", script);
    Assert.Contains("document.getElementById", script);
    Assert.Contains("input.type = reveal ? \"text\" : \"password\"", script);
    Assert.Contains("button.textContent = reveal ? \"Ẩn\" : \"Hiện\"", script);
    Assert.Contains("button.setAttribute(\"aria-label\"", script);
    Assert.DoesNotContain("submit", script, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the script test and verify it fails**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthScript_TogglesControlledPassword"
```

Expected: FAIL because the Task 1 bootstrap script does not contain password-toggle behavior.

- [ ] **Step 3: Create `wwwroot/js/auth.js`**

```javascript
(() => {
    document.querySelectorAll("[data-password-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            const inputId = button.getAttribute("aria-controls");
            const input = inputId ? document.getElementById(inputId) : null;

            if (!(input instanceof HTMLInputElement)) return;

            const reveal = input.type === "password";
            input.type = reveal ? "text" : "password";
            button.textContent = reveal ? "Ẩn" : "Hiện";
            button.setAttribute("aria-label", reveal ? "Ẩn mật khẩu" : "Hiện mật khẩu");
        });
    });
})();
```

- [ ] **Step 4: Run all auth layout tests**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~ltwnc.Tests.Views.AuthLayoutMarkupTests"
```

Expected: all auth layout tests PASS.

- [ ] **Step 5: Commit the password interaction**

```powershell
git add -- wwwroot/js/auth.js tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs
git commit -m "feat: add accessible auth password toggles"
```

---

### Task 5: Remove the throwaway prototype and run production verification

**Files:**
- Modify: `tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs`
- Modify: `Controllers/AccountController.cs`
- Delete: `Views/Account/AuthPrototype.cshtml`
- Delete: `Views/Account/Prototypes/_AuthVariantA.cshtml`
- Delete: `Views/Account/Prototypes/_AuthVariantB.cshtml`
- Delete: `Views/Account/Prototypes/_AuthVariantC.cshtml`
- Delete: `wwwroot/css/auth-prototype.css`
- Delete: `wwwroot/js/auth-prototype.js`

**Interfaces:**
- Consumes: completed production auth layout from Tasks 1–4.
- Produces: production tree with no prototype route/assets; unchanged GET/POST Login and Register controller API.

- [ ] **Step 1: Add the failing cleanup contract inside `AuthLayoutMarkupTests`**

```csharp
[Fact]
public void ProductionAuth_DoesNotRetainPrototypeRouteOrAssets()
{
    string controller = Read("Controllers/AccountController.cs");

    Assert.DoesNotContain("AuthPrototype", controller);
    Assert.False(File.Exists(Path.Combine(Root, "Views", "Account", "AuthPrototype.cshtml")));
    Assert.False(Directory.Exists(Path.Combine(Root, "Views", "Account", "Prototypes")));
    Assert.False(File.Exists(Path.Combine(Root, "wwwroot", "css", "auth-prototype.css")));
    Assert.False(File.Exists(Path.Combine(Root, "wwwroot", "js", "auth-prototype.js")));
}
```

- [ ] **Step 2: Run the cleanup test and verify it fails**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~ProductionAuth_DoesNotRetainPrototype"
```

Expected: FAIL because the development-only prototype still exists.

- [ ] **Step 3: Remove the prototype action and files**

Delete this exact block from `Controllers/AccountController.cs`:

```csharp
// PROTOTYPE ONLY: three read-only auth layouts for visual evaluation.
[HttpGet]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public IActionResult AuthPrototype([FromServices] IWebHostEnvironment environment)
{
    if (!environment.IsDevelopment())
    {
        return NotFound();
    }

    return View();
}
```

Delete the five prototype paths listed in this task's **Files** section. Do not remove `Views/Account/Login.cshtml`, `Views/Account/Register.cshtml`, `wwwroot/css/auth.css`, or `wwwroot/js/auth.js`.

- [ ] **Step 4: Run cleanup and controller tests**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter "FullyQualifiedName~ProductionAuth_DoesNotRetainPrototype|FullyQualifiedName~AccountControllerTests"
```

Expected: PASS.

- [ ] **Step 5: Start the app and verify the exact responsive matrix**

Run:

```powershell
dotnet run --no-build --launch-profile http
```

Use browser responsive inspection on both `/Account/Login` and `/Account/Register`:

1. 1440 × 900: `document.documentElement.scrollHeight === document.documentElement.clientHeight` before validation errors.
2. 1366 × 768: same no-scroll assertion and no clipped submit/alternate link.
3. 1024 × 768: form is readable; no horizontal overflow.
4. 390 × 844: form appears before the 220 px studio; vertical scroll is allowed; no horizontal overflow.
5. 200% zoom: labels, controls and errors remain reachable.
6. Disable the three auth image requests: sage fallback remains and the form layout does not move.
7. Submit invalid login/register forms: ModelState/client errors remain visible and the page may scroll.

Expected: all seven checks match the approved spec. Fix only `wwwroot/css/auth.css` for visual spacing defects; rerun the focused auth tests after each fix.

- [ ] **Step 6: Run build and the full test suite**

Run:

```powershell
dotnet build --no-restore
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-build --no-restore
```

Expected: build succeeds with 0 errors; full test suite PASS.

- [ ] **Step 7: Commit prototype cleanup and final verified state**

```powershell
git add -- tests/ltwnc.Tests/Views/AuthLayoutMarkupTests.cs
git add -u -- Controllers/AccountController.cs
git commit -m "test: enforce production auth cleanup"
```

Before committing, run `git diff --cached --name-only` and confirm it contains only `AuthLayoutMarkupTests.cs` plus `Controllers/AccountController.cs` if removing the prototype action leaves a tracked diff. The untracked prototype files are removed from the main worktree but preserved on `codex/prototype-auth-layout-c`.
