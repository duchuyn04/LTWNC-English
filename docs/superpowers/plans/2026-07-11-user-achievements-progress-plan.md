# User Achievements Progress Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand UserAchievements with count-tier badges, live progress (bar + current/target + CTA), Observer-driven unlock, and page-load rescan so learners always see accurate milestone status.

**Architecture:** Keep `AchievementCatalog` in code with metric metadata. `AchievementProgressService` loads a per-user metric snapshot. `AchievementUnlockService.SyncEligibleAsync` inserts missing `UserAchievement` rows. `AchievementStudyObserver` only calls the unlock service. `AchievementService` orchestrates rescan + view models for `/Achievements`. No new EF columns on `UserAchievement`.

**Tech Stack:** ASP.NET Core MVC (.NET 10), EF Core, Razor, xUnit + EF InMemory, MCP Playwright for browser E2E/responsive checks.

**Spec:** `docs/superpowers/specs/2026-07-11-user-achievements-progress-design.md`

---

## Global Constraints

- Preserve existing achievement **codes** (`first_card_mastered`, `cards_mastered_10`, `first_flashcard_session`, `first_dictation_session`, `dictation_perfect_session`).
- No XP, streak, set-mastery, admin catalog, or real-time toast on every MarkLearned.
- TempData banner only when **page rescan** unlocks new badges.
- Comment new/changed production code with clear Vietnamese `//` explanations (non-tech friendly where business logic is non-obvious).
- Unit tests first (TDD). Browser verification uses **MCP Playwright** (`playwright__browser_*` tools). Sign in with the test account supplied by the user at execution time; **do not store passwords** in this plan or source control.
- App base URL for E2E: `http://localhost:5000` (or the URL printed by `dotnet run`). Run `dotnet ef database update` if achievements table is missing.
- Responsive viewports: **desktop `1440x900`**, **mobile `390x844`**. Assert no horizontal overflow: `document.documentElement.scrollWidth <= document.documentElement.clientWidth`.

### MCP Playwright playbook (reuse in tasks)

```text
1. Ensure app is running: dotnet run --project ltwnc.csproj --launch-profile http
2. playwright__browser_navigate → login or target URL
3. playwright__browser_snapshot → discover targets (do not guess selectors blindly)
4. playwright__browser_fill_form / browser_type / browser_click → interact
5. playwright__browser_resize → width/height for responsive checks
6. playwright__browser_evaluate → overflow / progress width checks
7. playwright__browser_take_screenshot → evidence (type=png, scale=css; no credentials in filename text overlays)
```

Login flow (when needed):

```text
playwright__browser_navigate url=http://localhost:5000/Account/Login
playwright__browser_snapshot
playwright__browser_fill_form  Email + Password fields
playwright__browser_click      Login submit
playwright__browser_snapshot   confirm signed-in nav (e.g. "Thành tích")
```

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Services/StudyEvents/AchievementMetricKind.cs` | Metric enum |
| `Services/StudyEvents/AchievementCatalog.cs` | Definitions + targets + CTA |
| `Services/StudyEvents/AchievementProgressSnapshot.cs` | DTO of metric counts |
| `Services/AchievementProgressService.cs` | Query metrics once per user |
| `Services/AchievementUnlockService.cs` | Sync eligible unlocks; return newly unlocked definitions |
| `Services/StudyEvents/AchievementStudyObserver.cs` | Thin: call unlock service only |
| `Services/AchievementService.cs` | Rescan + map catalog to view models |
| `Models/ViewModels/Achievements/AchievementListItemViewModel.cs` | Progress + CTA fields |
| `Controllers/AchievementsController.cs` | TempData for new unlocks |
| `Views/Achievements/Index.cshtml` | Bar, CTA, banner; responsive CSS |
| `Program.cs` | DI for new services |
| `README.md` | Short note on progress/rescan |
| `tests/ltwnc.Tests/StudyEvents/*` | Unit tests for progress, unlock, observer, service |
| `tests/ltwnc.Tests/StudyEvents/AchievementStudyObserverTests.cs` | Update for thin observer |

No migration if `UserAchievement` entity stays unchanged.

---

### Task 1: Catalog model + metric enum (TDD)

**Files:**
- Create: `Services/StudyEvents/AchievementMetricKind.cs`
- Modify: `Services/StudyEvents/AchievementCatalog.cs`
- Create: `tests/ltwnc.Tests/StudyEvents/AchievementCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

```csharp
using ltwnc.Services.StudyEvents;

namespace ltwnc.Tests.StudyEvents;

// Kiểm tra danh mục huy hiệu đủ mốc count và metadata tiến độ
public class AchievementCatalogTests
{
    [Fact]
    public void All_contains_expected_medium_scope_codes()
    {
        var codes = AchievementCatalog.All.Select(d => d.Code).ToHashSet();
        Assert.Contains(AchievementCatalog.FirstCardMastered, codes);
        Assert.Contains(AchievementCatalog.CardsMastered10, codes);
        Assert.Contains("cards_mastered_25", codes);
        Assert.Contains("cards_mastered_50", codes);
        Assert.Contains("cards_mastered_100", codes);
        Assert.Contains("flashcard_sessions_5", codes);
        Assert.Contains("flashcard_sessions_10", codes);
        Assert.Contains("flashcard_sessions_20", codes);
        Assert.Contains("dictation_sessions_5", codes);
        Assert.Contains("dictation_correct_10", codes);
        Assert.Contains("dictation_correct_50", codes);
        Assert.Contains(AchievementCatalog.DictationPerfectSession, codes);
    }

    [Fact]
    public void Every_definition_has_positive_target_and_cta()
    {
        Assert.All(AchievementCatalog.All, d =>
        {
            Assert.True(d.Target > 0);
            Assert.False(string.IsNullOrWhiteSpace(d.CtaText));
            Assert.False(string.IsNullOrWhiteSpace(d.CtaPath));
            Assert.StartsWith("/", d.CtaPath);
        });
    }

    [Theory]
    [InlineData(AchievementCatalog.CardsMastered10, AchievementMetricKind.CardsMastered, 10)]
    [InlineData(AchievementCatalog.DictationPerfectSession, AchievementMetricKind.DictationPerfectSessions, 1)]
    public void Find_returns_metric_and_target(string code, AchievementMetricKind metric, int target)
    {
        var def = AchievementCatalog.Find(code);
        Assert.NotNull(def);
        Assert.Equal(metric, def!.Metric);
        Assert.Equal(target, def.Target);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~AchievementCatalogTests
```

Expected: compile errors (enum / Definition shape missing).

- [ ] **Step 3: Implement enum + catalog shape**

`Services/StudyEvents/AchievementMetricKind.cs`:

```csharp
namespace ltwnc.Services.StudyEvents;

// Loại chỉ số dùng để đo tiến độ huy hiệu (mỗi huy hiệu gắn một loại)
public enum AchievementMetricKind
{
    CardsMastered,
    FlashcardSessions,
    DictationSessions,
    DictationCorrectAnswers,
    DictationPerfectSessions
}
```

Rewrite `AchievementCatalog.Definition` to:

```csharp
public sealed record Definition(
    string Code,
    string Title,
    string Description,
    AchievementMetricKind Metric,
    int Target,
    string CtaText,
    string CtaPath);
```

Populate `All` with **all medium-scope rows** from the design spec (stable codes for existing five; add new string constants for new codes). Default CTA:

- Card metrics: `CtaText = "Học tiếp trong thư viện bộ thẻ"`, `CtaPath = "/Set"`
- Session/dictation metrics: `CtaText = "Chọn bộ thẻ để học tiếp"`, `CtaPath = "/Set"`

Keep public const strings for existing codes; add consts for new codes for test/code clarity.

- [ ] **Step 4: Fix compile breaks**

Update `AchievementStudyObserver.TryUnlockAsync` and any code constructing `Definition` — temporary: observer may still use old switch until Task 3; ensure it compiles with new `Definition` (pass metric/target when calling `Find` only).

- [ ] **Step 5: Run catalog tests — expect PASS**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~AchievementCatalogTests
```

- [ ] **Step 6: Commit**

```bash
git add Services/StudyEvents/AchievementMetricKind.cs Services/StudyEvents/AchievementCatalog.cs Services/StudyEvents/AchievementStudyObserver.cs tests/ltwnc.Tests/StudyEvents/AchievementCatalogTests.cs
git commit -m "feat(achievements): expand count-tier catalog with metrics"
```

---

### Task 2: Progress snapshot service (TDD)

**Files:**
- Create: `Services/StudyEvents/AchievementProgressSnapshot.cs`
- Create: `Services/AchievementProgressService.cs`
- Create: `tests/ltwnc.Tests/StudyEvents/AchievementProgressServiceTests.cs`
- Modify: `Program.cs` (register service)

- [ ] **Step 1: Write failing progress tests**

```csharp
// Seed: 3 mastered cards, 2 flashcard sessions, 1 dictation session score 100,
// 4 correct dictation details → assert snapshot fields.
[Fact]
public async Task GetSnapshotAsync_counts_all_metrics_for_user()
{
    // Arrange seed in InMemory AppDbContext for user "u1" only
    // Act
    var snapshot = await _sut.GetSnapshotAsync("u1");
    // Assert
    Assert.Equal(3, snapshot.CardsMastered);
    Assert.Equal(2, snapshot.FlashcardSessions);
    Assert.Equal(1, snapshot.DictationSessions);
    Assert.Equal(4, snapshot.DictationCorrectAnswers);
    Assert.Equal(1, snapshot.DictationPerfectSessions);
}

[Fact]
public async Task GetSnapshotAsync_does_not_count_other_users()
{
    // seed u1 and u2; only query u1
}
```

Helper on snapshot:

```csharp
public int GetValue(AchievementMetricKind kind) => kind switch
{
    AchievementMetricKind.CardsMastered => CardsMastered,
    AchievementMetricKind.FlashcardSessions => FlashcardSessions,
    AchievementMetricKind.DictationSessions => DictationSessions,
    AchievementMetricKind.DictationCorrectAnswers => DictationCorrectAnswers,
    AchievementMetricKind.DictationPerfectSessions => DictationPerfectSessions,
    _ => 0
};
```

- [ ] **Step 2: Run — expect FAIL**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~AchievementProgressServiceTests
```

- [ ] **Step 3: Implement snapshot + service**

Queries (per design):

```csharp
CardsMastered = await _context.UserProgresses.CountAsync(p => p.UserId == userId && p.IsLearned);
FlashcardSessions = await _context.StudySessions.CountAsync(s => s.UserId == userId && s.Mode == StudyMode.Flashcard);
DictationSessions = await _context.StudySessions.CountAsync(s => s.UserId == userId && s.Mode == StudyMode.Dictation);
DictationPerfectSessions = await _context.StudySessions.CountAsync(s => s.UserId == userId && s.Mode == StudyMode.Dictation && s.Score == 100);
DictationCorrectAnswers = await _context.DictationSessionDetails
    .CountAsync(d => d.IsCorrect && d.StudySession!.UserId == userId);
// Prefer join if navigation not loaded:
// from d in DictationSessionDetails join s in StudySessions on d.StudySessionId equals s.Id
// where s.UserId == userId && d.IsCorrect
```

Add Vietnamese comments on class/methods.

- [ ] **Step 4: Register DI**

```csharp
builder.Services.AddScoped<AchievementProgressService>();
```

- [ ] **Step 5: Tests PASS + commit**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~AchievementProgressServiceTests
git add Services/AchievementProgressService.cs Services/StudyEvents/AchievementProgressSnapshot.cs Program.cs tests/ltwnc.Tests/StudyEvents/AchievementProgressServiceTests.cs
git commit -m "feat(achievements): add live metric progress snapshot"
```

---

### Task 3: Unlock service + thin Observer (TDD)

**Files:**
- Create: `Services/AchievementUnlockService.cs`
- Modify: `Services/StudyEvents/AchievementStudyObserver.cs`
- Modify: `tests/ltwnc.Tests/StudyEvents/AchievementStudyObserverTests.cs`
- Create: `tests/ltwnc.Tests/StudyEvents/AchievementUnlockServiceTests.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Write unlock service tests**

```csharp
[Fact]
public async Task SyncEligibleAsync_unlocks_all_tiers_met_not_higher()
{
    // Seed 25 mastered cards, zero UserAchievements
    var newly = await _sut.SyncEligibleAsync("u1");
    var codes = await _context.UserAchievements.Where(a => a.UserId == "u1").Select(a => a.Code).ToListAsync();
    Assert.Contains(AchievementCatalog.FirstCardMastered, codes);
    Assert.Contains(AchievementCatalog.CardsMastered10, codes);
    Assert.Contains("cards_mastered_25", codes);
    Assert.DoesNotContain("cards_mastered_50", codes);
    Assert.Contains(AchievementCatalog.FirstCardMastered, newly.Select(d => d.Code));
}

[Fact]
public async Task SyncEligibleAsync_is_idempotent()
{
    await _sut.SyncEligibleAsync("u1");
    var second = await _sut.SyncEligibleAsync("u1");
    Assert.Empty(second);
    Assert.Equal(1, await _context.UserAchievements.CountAsync(a => a.UserId == "u1" && a.Code == AchievementCatalog.FirstCardMastered));
}

[Fact]
public async Task SyncEligibleAsync_unlocks_perfect_dictation()
{
    // Seed StudySession Mode=Dictation Score=100
}
```

- [ ] **Step 2: Implement `AchievementUnlockService`**

```csharp
public async Task<IReadOnlyList<AchievementCatalog.Definition>> SyncEligibleAsync(
    string userId,
    CancellationToken cancellationToken = default)
{
    var snapshot = await _progress.GetSnapshotAsync(userId, cancellationToken);
    var already = await _context.UserAchievements
        .Where(a => a.UserId == userId)
        .Select(a => a.Code)
        .ToListAsync(cancellationToken);
    var have = already.ToHashSet();
    var newly = new List<AchievementCatalog.Definition>();

    foreach (var def in AchievementCatalog.All)
    {
        if (have.Contains(def.Code)) continue;
        var value = snapshot.GetValue(def.Metric);
        if (value < def.Target) continue;

        _context.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            Code = def.Code,
            Title = def.Title,
            Description = def.Description,
            UnlockedAt = DateTime.UtcNow
        });
        newly.Add(def);
        have.Add(def.Code);
    }

    if (newly.Count > 0)
    {
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { /* unique race: treat as already unlocked */ }
    }
    return newly;
}
```

- [ ] **Step 3: Thin Observer**

```csharp
public async Task OnStudyEventAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
{
    // Mọi sự kiện học đều quét lại huy hiệu đủ điều kiện cho user đó
    if (string.IsNullOrWhiteSpace(studyEvent.UserId)) return;
    await _unlockService.SyncEligibleAsync(studyEvent.UserId, cancellationToken);
}
```

Inject `AchievementUnlockService`. Remove private tier methods.

- [ ] **Step 4: Update observer tests** to inject unlock service + progress (or construct real services with InMemory context). Keep: first card unlock, no duplicate, flashcard session, perfect dictation.

- [ ] **Step 5: DI + run tests**

```csharp
builder.Services.AddScoped<AchievementUnlockService>();
```

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~AchievementUnlock|FullyQualifiedName~AchievementStudyObserver"
```

- [ ] **Step 6: Commit**

```bash
git add Services/AchievementUnlockService.cs Services/StudyEvents/AchievementStudyObserver.cs Program.cs tests/ltwnc.Tests/StudyEvents/
git commit -m "feat(achievements): centralize unlock sync for Observer"
```

---

### Task 4: AchievementService + ViewModel + Controller TempData (TDD)

**Files:**
- Modify: `Models/ViewModels/Achievements/AchievementListItemViewModel.cs`
- Modify: `Services/AchievementService.cs`
- Modify: `Controllers/AchievementsController.cs`
- Create: `tests/ltwnc.Tests/Services/AchievementServiceTests.cs`

- [ ] **Step 1: Expand view model**

```csharp
public class AchievementListItemViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int Current { get; set; }
    public int Target { get; set; }
    public int ProgressPercent { get; set; }
    public string CtaText { get; set; } = string.Empty;
    public string CtaUrl { get; set; } = string.Empty;
}
```

Change service return type to a result object:

```csharp
public sealed class AchievementPageModel
{
    public IReadOnlyList<AchievementListItemViewModel> Items { get; init; } = [];
    public IReadOnlyList<string> NewlyUnlockedTitles { get; init; } = [];
}
```

- [ ] **Step 2: Write service tests**

```csharp
[Fact]
public async Task GetPageAsync_includes_progress_for_locked_badge()
{
    // 7 mastered cards, no achievements rows
    var page = await _sut.GetPageAsync("u1");
    var ten = page.Items.Single(i => i.Code == AchievementCatalog.CardsMastered10);
    Assert.False(ten.IsUnlocked);
    Assert.Equal(7, ten.Current);
    Assert.Equal(10, ten.Target);
    Assert.Equal(70, ten.ProgressPercent);
    Assert.Equal("/Set", ten.CtaUrl);
}

[Fact]
public async Task GetPageAsync_rescan_unlocks_and_reports_new_titles()
{
    // 10 mastered, zero rows → after GetPageAsync rows exist and NewlyUnlockedTitles non-empty
}
```

- [ ] **Step 3: Implement `GetPageAsync`**

```csharp
public async Task<AchievementPageModel> GetPageAsync(string userId, CancellationToken ct = default)
{
    var newly = await _unlock.SyncEligibleAsync(userId, ct);
    var snapshot = await _progress.GetSnapshotAsync(userId, ct);
    var unlocked = await _context.UserAchievements.AsNoTracking()
        .Where(a => a.UserId == userId)
        .ToDictionaryAsync(a => a.Code, a => a.UnlockedAt, ct);

    var items = AchievementCatalog.All.Select(def =>
    {
        var value = snapshot.GetValue(def.Metric);
        var current = Math.Min(value, def.Target);
        var isUnlocked = unlocked.ContainsKey(def.Code);
        return new AchievementListItemViewModel
        {
            Code = def.Code,
            Title = def.Title,
            Description = def.Description,
            IsUnlocked = isUnlocked,
            UnlockedAt = isUnlocked ? unlocked[def.Code] : null,
            Current = isUnlocked ? def.Target : current,
            Target = def.Target,
            ProgressPercent = def.Target <= 0 ? 0 : (isUnlocked ? 100 : current * 100 / def.Target),
            CtaText = def.CtaText,
            CtaUrl = def.CtaPath
        };
    })
    .OrderByDescending(i => i.IsUnlocked)
    .ThenByDescending(i => i.ProgressPercent)
    .ThenBy(i => i.Title)
    .ToList();

    return new AchievementPageModel
    {
        Items = items,
        NewlyUnlockedTitles = newly.Select(d => d.Title).ToList()
    };
}
```

- [ ] **Step 4: Controller**

```csharp
var page = await _achievementService.GetPageAsync(user.Id);
if (page.NewlyUnlockedTitles.Count > 0)
{
    TempData["AchievementUnlock"] = "Bạn vừa mở: " + string.Join(", ", page.NewlyUnlockedTitles);
}
return View(page.Items);
```

- [ ] **Step 5: Unit tests PASS + commit**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~AchievementService
git add Models/ViewModels/Achievements/ Services/AchievementService.cs Controllers/AchievementsController.cs tests/ltwnc.Tests/Services/AchievementServiceTests.cs
git commit -m "feat(achievements): rescan page model with progress fields"
```

---

### Task 5: Razor UI — progress bar, CTA, banner + responsive CSS

**Files:**
- Modify: `Views/Achievements/Index.cshtml`
- Optional: extract CSS to `wwwroot/css/achievements.css` and link in view (prefer scoped `@section Styles` to match current page)

#### UI requirements (must implement)

1. **Banner:** if `TempData["AchievementUnlock"]` is string → alert at top.
2. **Locked card:** class `ach-card is-locked`; show:
   - `data-code="@item.Code"` on card for E2E.
   - Progress text: `@item.Current / @item.Target`
   - Progress bar track + fill with `style="width: @item.ProgressPercent%"` and `role="progressbar"` `aria-valuenow` `aria-valuemin="0"` `aria-valuemax="100"`.
   - CTA: `<a class="ach-cta" href="@item.CtaUrl">@item.CtaText</a>`
3. **Unlocked card:** medal icon; unlock time; optional full bar 100%; **hide CTA** or show muted “Đã hoàn thành”.
4. **Responsive CSS:**
   - `.ach-page { max-width: 720px; padding: ... }`
   - Mobile: `@media (max-width: 480px)` reduce padding; stack icon+body; CTA `display:inline-flex; min-height: 44px;` full-width button optional.
   - Progress bar min height 8px; track full width of card content.
   - Prevent overflow: `box-sizing: border-box; max-width: 100%; word-break` on titles.
   - Nav link “Thành tích” already exists — ensure page title readable on small screens (`h1` font-size clamp).

Suggested markup snippet:

```html
@if (TempData["AchievementUnlock"] is string unlockMsg)
{
    <div class="ach-banner" role="status">@unlockMsg</div>
}

<div class="ach-card @(item.IsUnlocked ? "is-unlocked" : "is-locked")" data-code="@item.Code" data-unlocked="@item.IsUnlocked.ToString().ToLowerInvariant()">
  ...
  @if (!item.IsUnlocked)
  {
    <div class="ach-progress">
      <div class="ach-progress__meta">@item.Current / @item.Target</div>
      <div class="ach-progress__track">
        <div class="ach-progress__fill" role="progressbar"
             aria-valuenow="@item.ProgressPercent" aria-valuemin="0" aria-valuemax="100"
             style="width: @(item.ProgressPercent)%"></div>
      </div>
    </div>
    <a class="ach-cta" href="@item.CtaUrl">@item.CtaText</a>
  }
</div>
```

- [ ] **Step 1: Implement view + CSS**

- [ ] **Step 2: Manual smoke**

```bash
dotnet build
dotnet run --project ltwnc.csproj --launch-profile http
```

Open `/Achievements` while logged in; confirm layout.

- [ ] **Step 3: MCP Playwright — desktop smoke (E2E)**

Prereq: app running; user-supplied test account credentials (not stored here).

1. `playwright__browser_navigate` → `http://localhost:5000/Account/Login`
2. Snapshot → fill login → submit → confirm nav contains **Thành tích**
3. `playwright__browser_resize` width=`1440` height=`900`
4. Navigate `/Achievements`
5. `playwright__browser_snapshot` — assert:
   - Heading “Thành tích”
   - At least one `[data-code]` card
   - Locked card shows text matching `/\d+\s*\/\s*\d+/` and a CTA link to `/Set`
6. `playwright__browser_evaluate`:

```js
() => ({
  overflow: document.documentElement.scrollWidth <= document.documentElement.clientWidth,
  cards: document.querySelectorAll('.ach-card').length,
  bars: document.querySelectorAll('[role="progressbar"]').length
})
```

Expected: `overflow === true`, `cards >= 10`, `bars >= 1` (unless all unlocked).

7. `playwright__browser_take_screenshot` filename=`achievements-desktop-1440.png` type=`png` scale=`css` fullPage=`true`

- [ ] **Step 4: MCP Playwright — mobile responsive (E2E)**

1. `playwright__browser_resize` width=`390` height=`844`
2. Reload `/Achievements`
3. Evaluate overflow again — must be `true`
4. Evaluate CTA hit target:

```js
() => {
  const cta = document.querySelector('.ach-cta');
  if (!cta) return { hasCta: false };
  const r = cta.getBoundingClientRect();
  return { hasCta: true, height: r.height, width: r.width, fullyInView: r.left >= 0 && r.right <= window.innerWidth + 1 };
}
```

Expected: height ≥ 40, fullyInView true, no horizontal clip.

5. Screenshot `achievements-mobile-390.png` fullPage true
6. Click nav **Thành tích** (if visible) or open hamburger if layout collapses — if navbar collapses on mobile, open toggler then click Thành tích; snapshot proves navigation still works at 390px.

- [ ] **Step 5: Commit UI**

```bash
git add Views/Achievements/Index.cshtml
git commit -m "feat(achievements): progress bars, CTAs, and responsive layout"
```

---

### Task 6: E2E rescan unlock + CTA navigation (MCP Playwright)

**Goal:** Prove page-load rescan and CTA behavior end-to-end (not only unit tests).

**Prep (SQL or UI — choose one at execution):**

**Option A — UI (preferred if account owns a set with cards):**  
Mark enough cards learned via flashcard UI so `CardsMastered >= 1` but ensure no reliance on Observer alone: optionally delete that user’s `UserAchievements` rows in DB for a clean rescan, **or** use a fresh test user with progress seeded.

**Option B — seed SQL (dev only):** insert `UserProgress` rows `IsLearned=1` for the test user without corresponding `UserAchievements`.

- [ ] **Step 1: Establish pre-rescan state**

Document in execution notes which option was used. Confirm via unit path or SQL that metric ≥ 1 and achievement row missing for `first_card_mastered` if testing rescan banner.

- [ ] **Step 2: Playwright rescan flow**

1. Login as test user  
2. Desktop resize 1440×900  
3. Navigate `/Achievements`  
4. Snapshot: if rescan unlocked, banner text contains `Bạn vừa mở`  
5. Card `[data-code="first_card_mastered"]` has `data-unlocked="true"`  
6. Reload `/Achievements` — banner should **not** reappear (second sync returns empty newly list); card stays unlocked  

- [ ] **Step 3: CTA click**

1. Find a locked card with `.ach-cta`  
2. `playwright__browser_click` the CTA  
3. Snapshot URL is `/Set` (or library page)  
4. Mobile 390×844: repeat CTA click; confirm destination still correct and no overflow on destination header  

- [ ] **Step 4: Capture evidence screenshots**

- `achievements-rescan-banner.png` (if banner shown)  
- `achievements-after-cta-set.png`  

- [ ] **Step 5: Commit** only if code fixes arose; otherwise no code commit — note E2E pass in PR/description.

If E2E fails due to missing `data-code` attributes, fix view (Task 5) and re-run.

---

### Task 7: Full regression unit suite + Observer path smoke + README

**Files:**
- Modify: `README.md` (Observer section: progress + rescan)
- All tests

- [ ] **Step 1: Run full unit tests**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
```

Expected: all green (including old clone/strategy/command tests).

- [ ] **Step 2: Observer integration unit test (optional if not already)**

Publish `CardProgressChangedEvent` through real `StudyEventPublisher` with `AchievementStudyObserver` registered → assert unlock row created when metrics sufficient.

- [ ] **Step 3: MCP Playwright final responsive matrix**

| Viewport | URL | Checks |
|----------|-----|--------|
| 1440×900 | `/Achievements` | overflow OK; hero readable; bars visible |
| 390×844 | `/Achievements` | overflow OK; CTA tappable; progress meta not clipped |
| 1440×900 | `/Set` via CTA | no broken layout regression |
| 390×844 | Login → Achievements via nav | nav usable |

Screenshots: `achievements-final-desktop.png`, `achievements-final-mobile.png`

- [ ] **Step 4: README**

Update Observer bullet list:

- Progress computed live (`AchievementProgressService`)
- Unlock via `AchievementUnlockService` (Observer + page rescan)
- UI shows bar, current/target, CTA

- [ ] **Step 5: Final commit**

```bash
git add README.md tests/
git commit -m "docs(achievements): document progress metrics and rescan"
```

- [ ] **Step 6: Optional push** (only if user requests)

```bash
git push origin master
```

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| Medium count catalog | Task 1 |
| Live metrics queries | Task 2 |
| Unlock tiers + idempotent | Task 3 |
| Thin Observer | Task 3 |
| Page rescan + NewlyUnlocked | Task 4 |
| Progress fields on VM | Task 4 |
| TempData banner | Task 4–5 |
| Progress bar + CTA UI | Task 5 |
| Responsive UI | Task 5–6–7 (Playwright) |
| E2E rescan + CTA | Task 6 |
| No entity migration | All tasks |
| No XP/streak/set mastery | Out of scope (not scheduled) |

## Placeholder / consistency self-review

- Method name standardized: `GetPageAsync` on `AchievementService` (controller updated in Task 4).
- `SyncEligibleAsync` returns `IReadOnlyList<AchievementCatalog.Definition>`.
- Snapshot `GetValue(AchievementMetricKind)` used by unlock + page mapping.
- Playwright tools named with `playwright__` prefix as available in this environment.
- No TBD steps; passwords not in plan.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-11-user-achievements-progress-plan.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — execute tasks in this session with checkpoints  

Which approach do you want? (Also provide a test login account at E2E tasks if not already available.)
