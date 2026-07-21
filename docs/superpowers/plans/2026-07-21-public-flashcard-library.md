# Public Flashcard Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Chuyển prototype phương án A thành `/Library` công khai, dùng dữ liệu thật với tìm kiếm, sắp xếp, phân trang và thống kê aggregate.

**Architecture:** Một `PublicLibraryService` chỉ đọc thực hiện projection/aggregate trực tiếp từ EF Core và không mở rộng service CRUD hiện hữu. `LibraryController` map kết quả sang view model Razor; giao diện production tái sử dụng ngôn ngữ thiết kế phương án A rồi xóa toàn bộ artefact prototype UI.

**Tech Stack:** .NET 10, ASP.NET Core MVC, Razor, EF Core 10, SQL Server, xUnit, Moq, `WebApplicationFactory<Program>`.

## Global Constraints

- `/Library` cho phép truy cập ẩn danh; `/Set` vẫn là thư viện cá nhân có xác thực.
- Chỉ hiển thị set `IsPublic == true` và `ModerationStatus == Active`.
- Không expose email tác giả.
- Không thêm migration, topic/category, favorite hoặc endpoint thay đổi dữ liệu.
- Tìm kiếm, sắp xếp và phân trang hoạt động khi JavaScript tắt.
- Không xóa `Models/IPrototype.cs`.
- Mỗi trang có đúng 12 kết quả.

---

### Task 1: Public-library query service

**Files:**
- Create: `Services/PublicLibrary/PublicLibraryModels.cs`
- Create: `Services/PublicLibrary/IPublicLibraryService.cs`
- Create: `Services/PublicLibrary/PublicLibraryService.cs`
- Create: `tests/ltwnc.Tests/Services/PublicLibrary/PublicLibraryServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.FlashcardSets`, `AppDbContext.Flashcards`, `AppDbContext.Users`, `FlashcardSet.SourceSetId`.
- Produces: `Task<PublicLibraryResult> BrowseAsync(PublicLibraryQuery query, CancellationToken cancellationToken = default)`.

- [x] **Step 1: Write failing query tests**

Create a test fixture with EF InMemory and seed users plus active, private, quarantined and copied sets. Include these tests:

```csharp
[Fact]
public async Task BrowseAsync_ExcludesPrivateAndQuarantinedSetsFromItemsAndSummary()
{
    await SeedUserAsync("author", "minhanh");
    FlashcardSet visible = await SeedSetAsync("author", "Visible", isPublic: true);
    await SeedCardsAsync(visible.Id, 2);
    await SeedSetAsync("author", "Private", isPublic: false);
    await SeedSetAsync("author", "Quarantined", isPublic: true,
        moderationStatus: FlashcardSetModerationStatus.Quarantined);
    await SeedCopyAsync(visible.Id, "learner");

    PublicLibraryResult result = await _service.BrowseAsync(new(null, "popular", 1));

    PublicLibrarySetItem item = Assert.Single(result.Items);
    Assert.Equal("Visible", item.Title);
    Assert.Equal("minhanh", item.AuthorName);
    Assert.Equal(2, item.CardCount);
    Assert.Equal(1, item.CopyCount);
    Assert.Equal(new PublicLibrarySummary(1, 2, 1), result.Summary);
}

[Theory]
[InlineData("academic", "Academic words")]
[InlineData("writing", "Academic words")]
[InlineData("minhanh", "Academic words")]
public async Task BrowseAsync_SearchesTitleDescriptionAndAuthorCaseInsensitively(
    string search,
    string expectedTitle)
{
    await SeedUserAsync("author", "MinhAnh");
    await SeedSetAsync("author", expectedTitle, true, description: "IELTS Writing vocabulary");
    await SeedSetAsync("author", "Travel", true, description: "Airport phrases");

    PublicLibraryResult result = await _service.BrowseAsync(new(search.ToUpperInvariant(), "recent", 1));

    Assert.Equal(expectedTitle, Assert.Single(result.Items).Title);
    Assert.Equal(search.ToLowerInvariant(), result.Search);
}

[Fact]
public async Task BrowseAsync_NormalizesSortAndClampsPageToLastPage()
{
    await SeedUserAsync("author", "author");
    for (int index = 0; index < 13; index++)
    {
        await SeedSetAsync("author", $"Set {index:00}", true,
            updatedAt: new DateTime(2026, 7, 1).AddDays(index));
    }

    PublicLibraryResult result = await _service.BrowseAsync(new(null, "invalid", 99));

    Assert.Equal("popular", result.Sort);
    Assert.Equal(2, result.Page);
    Assert.Equal(12, result.PageSize);
    Assert.Equal(13, result.TotalItems);
    Assert.Equal(2, result.TotalPages);
    Assert.Single(result.Items);
}
```

Add deterministic order tests for `popular`, `recent` and `cards`, with different copy counts, timestamps and card counts. Each assertion must compare the complete ordered title sequence.

```csharp
[Theory]
[InlineData("popular", "Popular,Large,Recent")]
[InlineData("recent", "Recent,Large,Popular")]
[InlineData("cards", "Large,Recent,Popular")]
public async Task BrowseAsync_AppliesDeterministicSort(string sort, string expected)
{
    await SeedUserAsync("author", "author");
    FlashcardSet popular = await SeedSetAsync("author", "Popular", true,
        updatedAt: new DateTime(2026, 7, 1));
    FlashcardSet large = await SeedSetAsync("author", "Large", true,
        updatedAt: new DateTime(2026, 7, 10));
    FlashcardSet recent = await SeedSetAsync("author", "Recent", true,
        updatedAt: new DateTime(2026, 7, 20));
    await SeedCardsAsync(popular.Id, 1);
    await SeedCardsAsync(large.Id, 5);
    await SeedCardsAsync(recent.Id, 1);
    await SeedCopyAsync(popular.Id, "copy-1");
    await SeedCopyAsync(popular.Id, "copy-2");
    await SeedCopyAsync(popular.Id, "copy-3");
    await SeedCopyAsync(large.Id, "copy-4");

    PublicLibraryResult result = await _service.BrowseAsync(new(null, sort, 1));

    Assert.Equal(expected.Split(','), result.Items.Select(item => item.Title));
}
```

Use these complete fixture helpers so seeded copies cannot accidentally appear as public library results:

```csharp
private async Task SeedUserAsync(string id, string userName)
{
    _db.Users.Add(new IdentityUser { Id = id, UserName = userName });
    await _db.SaveChangesAsync();
}

private async Task<FlashcardSet> SeedSetAsync(
    string userId,
    string title,
    bool isPublic,
    string? description = null,
    string moderationStatus = FlashcardSetModerationStatus.Active,
    DateTime? updatedAt = null)
{
    var set = new FlashcardSet
    {
        UserId = userId,
        Title = title,
        Description = description,
        IsPublic = isPublic,
        ModerationStatus = moderationStatus,
        CreatedAt = updatedAt ?? new DateTime(2026, 7, 1),
        UpdatedAt = updatedAt ?? new DateTime(2026, 7, 1)
    };
    _db.FlashcardSets.Add(set);
    await _db.SaveChangesAsync();
    return set;
}

private async Task SeedCardsAsync(int setId, int count)
{
    for (int index = 0; index < count; index++)
    {
        _db.Flashcards.Add(new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = $"Front {setId}-{index}",
            BackText = $"Back {setId}-{index}",
            OrderIndex = index
        });
    }
    await _db.SaveChangesAsync();
}

private async Task SeedCopyAsync(int sourceSetId, string userId)
{
    _db.FlashcardSets.Add(new FlashcardSet
    {
        UserId = userId,
        Title = $"Copy {userId}",
        IsPublic = false,
        SourceSetId = sourceSetId
    });
    await _db.SaveChangesAsync();
}
```

- [x] **Step 2: Run service tests and verify RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~PublicLibraryServiceTests
```

Expected: FAIL because `Services.PublicLibrary` types do not exist.

- [x] **Step 3: Add immutable query contracts**

Create `Services/PublicLibrary/PublicLibraryModels.cs`:

```csharp
namespace ltwnc.Services.PublicLibrary;

public static class PublicLibrarySort
{
    public const string Popular = "popular";
    public const string Recent = "recent";
    public const string Cards = "cards";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Recent => Recent,
        Cards => Cards,
        _ => Popular
    };
}

public sealed record PublicLibraryQuery(string? Search, string? Sort, int Page = 1);
public sealed record PublicLibrarySummary(int SetCount, int CardCount, int CopyCount);
public sealed record PublicLibrarySetItem(
    int Id,
    string Title,
    string? Description,
    string AuthorName,
    int CardCount,
    int CopyCount,
    DateTime UpdatedAt);

public sealed record PublicLibraryResult(
    string? Search,
    string Sort,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    PublicLibrarySummary Summary,
    IReadOnlyList<PublicLibrarySetItem> Items);
```

Create `Services/PublicLibrary/IPublicLibraryService.cs`:

```csharp
namespace ltwnc.Services.PublicLibrary;

public interface IPublicLibraryService
{
    Task<PublicLibraryResult> BrowseAsync(
        PublicLibraryQuery query,
        CancellationToken cancellationToken = default);
}
```

- [x] **Step 4: Implement normalized EF projection and aggregates**

Create `PublicLibraryService` with `PageSize = 12`. Normalize search using `Trim().ToLowerInvariant()`, build `visibleSets` before search, and project with a left join to `_db.Users`. The implementation body is:

```csharp
IQueryable<FlashcardSet> visibleSets = _db.FlashcardSets
    .AsNoTracking()
    .Where(set => set.IsPublic &&
        set.ModerationStatus == FlashcardSetModerationStatus.Active);

PublicLibrarySummary summary = new(
    await visibleSets.CountAsync(cancellationToken),
    await _db.Flashcards.AsNoTracking().CountAsync(
        card => visibleSets.Any(set => set.Id == card.FlashcardSetId),
        cancellationToken),
    await _db.FlashcardSets.AsNoTracking().CountAsync(
        copy => copy.SourceSetId.HasValue &&
            visibleSets.Any(set => set.Id == copy.SourceSetId.Value),
        cancellationToken));

IQueryable<PublicLibrarySetItem> projected =
    from set in visibleSets
    join author in _db.Users.AsNoTracking() on set.UserId equals author.Id into authors
    from author in authors.DefaultIfEmpty()
    select new PublicLibrarySetItem(
        set.Id,
        set.Title,
        set.Description,
        author != null && author.UserName != null ? author.UserName : "Thành viên",
        set.Flashcards.Count,
        _db.FlashcardSets.Count(copy => copy.SourceSetId == set.Id),
        set.UpdatedAt);

string? search = string.IsNullOrWhiteSpace(query.Search)
    ? null
    : query.Search.Trim().ToLowerInvariant();
if (search != null)
{
    projected = projected.Where(item =>
        item.Title.ToLower().Contains(search) ||
        (item.Description != null && item.Description.ToLower().Contains(search)) ||
        item.AuthorName.ToLower().Contains(search));
}

int totalItems = await projected.CountAsync(cancellationToken);
string sort = PublicLibrarySort.Normalize(query.Sort);
```

Apply search before counting filtered rows. Apply sorting only through a switch on normalized constants:

```csharp
IQueryable<PublicLibrarySetItem> ordered = sort switch
{
    PublicLibrarySort.Recent => projected
        .OrderByDescending(item => item.UpdatedAt)
        .ThenBy(item => item.Id),
    PublicLibrarySort.Cards => projected
        .OrderByDescending(item => item.CardCount)
        .ThenByDescending(item => item.UpdatedAt)
        .ThenBy(item => item.Id),
    _ => projected
        .OrderByDescending(item => item.CopyCount)
        .ThenByDescending(item => item.UpdatedAt)
        .ThenBy(item => item.Id)
};

int totalPages = (totalItems + PageSize - 1) / PageSize;
int page = totalPages == 0 ? 1 : Math.Clamp(query.Page, 1, totalPages);
List<PublicLibrarySetItem> items = await ordered
    .Skip((page - 1) * PageSize)
    .Take(PageSize)
    .ToListAsync(cancellationToken);

return new PublicLibraryResult(
    search,
    sort,
    page,
    PageSize,
    totalItems,
    totalPages,
    summary,
    items);
```

- [x] **Step 5: Run service tests and verify GREEN**

Run the focused test command from Step 2.

Expected: all `PublicLibraryServiceTests` pass with zero failures.

- [x] **Step 6: Commit the service slice**

```powershell
git add -- Services/PublicLibrary tests/ltwnc.Tests/Services/PublicLibrary
git commit -m "feat(library): add public set query service"
```

---

### Task 2: Anonymous MVC route and view model

**Files:**
- Create: `Models/ViewModels/Library/LibraryIndexViewModel.cs`
- Create: `Controllers/LibraryController.cs`
- Modify: `Program.cs`
- Create: `tests/ltwnc.Tests/Controllers/LibraryControllerTests.cs`
- Create: `tests/ltwnc.Tests/Integration/PublicLibraryRouteTests.cs`

**Interfaces:**
- Consumes: `IPublicLibraryService.BrowseAsync` from Task 1.
- Produces: anonymous `GET /Library`; `LibraryIndexViewModel.FromResult(PublicLibraryResult result)`.

- [x] **Step 1: Write failing controller tests**

Cover query forwarding and model mapping:

```csharp
[Fact]
public async Task Index_ForwardsQueryAndReturnsMappedModel()
{
    var service = new Mock<IPublicLibraryService>();
    var result = new PublicLibraryResult(
        "ielts", "recent", 2, 12, 13, 2,
        new PublicLibrarySummary(20, 300, 40),
        [new PublicLibrarySetItem(7, "IELTS", null, "minhanh", 20, 4, new DateTime(2026, 7, 20))]);
    service.Setup(item => item.BrowseAsync(
            new PublicLibraryQuery(" IELTS ", "recent", 2),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(result);
    var controller = new LibraryController(service.Object);

    ViewResult response = Assert.IsType<ViewResult>(
        await controller.Index(" IELTS ", "recent", 2, default));

    LibraryIndexViewModel model = Assert.IsType<LibraryIndexViewModel>(response.Model);
    Assert.Equal("ielts", model.Search);
    Assert.Equal(7, Assert.Single(model.Items).Id);
}
```

Add a reflection assertion that `LibraryController` or its action has `AllowAnonymousAttribute` and the action route is `/Library`.

- [x] **Step 2: Verify controller tests RED**

Run:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~LibraryControllerTests
```

Expected: FAIL because controller and view model do not exist.

- [x] **Step 3: Add the view model mapper**

Create `LibraryIndexViewModel` and `LibrarySetCardViewModel`. Preserve the normalized query/result fields. Compute presentation-only values as follows:

```csharp
public string AuthorInitials => string.Join(string.Empty, AuthorName
    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
    .Take(2)
    .Select(part => char.ToUpperInvariant(part[0])));

public string AccentClass => $"library-accent-{new[] { "sage", "brass", "clay", "sky", "plum", "moss" }[Math.Abs(Id) % 6]}";
```

Fallback to `TV` when computed initials are empty. Expose `HasPreviousPage` and `HasNextPage` from page metadata.

- [x] **Step 4: Add controller and DI registration**

Create:

```csharp
[AllowAnonymous]
public sealed class LibraryController : Controller
{
    private readonly IPublicLibraryService _libraryService;

    public LibraryController(IPublicLibraryService libraryService) =>
        _libraryService = libraryService;

    [HttpGet("/Library")]
    public async Task<IActionResult> Index(
        string? q,
        string? sort,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        PublicLibraryResult result = await _libraryService.BrowseAsync(
            new PublicLibraryQuery(q, sort, page),
            cancellationToken);
        return View(LibraryIndexViewModel.FromResult(result));
    }
}
```

Register `IPublicLibraryService` with scoped lifetime beside the flashcard-set services in `Program.cs`.

- [x] **Step 5: Add anonymous route integration test**

Use `WebApplicationFactory<Program>`, replace `IPublicLibraryService` with a singleton mock result, disable redirects, and assert:

```csharp
HttpResponseMessage response = await client.GetAsync("/Library?q=ielts&sort=recent&page=1");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

Also assert unauthenticated `GET /Set` remains a redirect to login.

- [x] **Step 6: Run controller and route tests GREEN, then commit**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~LibraryControllerTests|FullyQualifiedName~PublicLibraryRouteTests"
git add -- Controllers/LibraryController.cs Models/ViewModels/Library Program.cs tests/ltwnc.Tests/Controllers/LibraryControllerTests.cs tests/ltwnc.Tests/Integration/PublicLibraryRouteTests.cs
git commit -m "feat(library): expose anonymous browse route"
```

Expected: focused tests pass; commit contains no other files.

---

### Task 3: Production Razor UI and navigation

**Files:**
- Create: `Views/Library/Index.cshtml`
- Create: `wwwroot/css/library.css`
- Create: `wwwroot/js/library.js`
- Modify: `Views/Shared/_Layout.cshtml`
- Modify: `Views/Home/Index.cshtml`
- Create: `tests/ltwnc.Tests/Views/PublicLibraryMarkupTests.cs`

**Interfaces:**
- Consumes: `LibraryIndexViewModel` from Task 2.
- Produces: accessible server-rendered library UI with GET search, sort links/select and pager.

- [x] **Step 1: Write failing source-markup tests**

Assert the production view contains:

```csharp
Assert.Contains("method=\"get\"", view);
Assert.Contains("name=\"q\"", view);
Assert.Contains("name=\"sort\"", view);
Assert.Contains("Model.Summary.SetCount", view);
Assert.Contains("Model.Summary.CardCount", view);
Assert.Contains("Model.Summary.CopyCount", view);
Assert.Contains("asp-route-page", view);
Assert.Contains("/Set/@item.Id", view);
Assert.DoesNotContain("PROTOTYPE", view);
Assert.DoesNotContain("data-variant", view);
Assert.DoesNotContain("data-favorite", view);
Assert.DoesNotContain("IELTS Academic 7.0+", view);
```

Assert layout contains public `/Library` in both header and footer while preserving authenticated `/Set`. Assert the home exploration CTA points to `/Library`.

- [x] **Step 2: Verify markup tests RED**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~PublicLibraryMarkupTests
```

Expected: FAIL because production view/assets/navigation do not exist.

- [x] **Step 3: Build the production Razor view**

Use `@section Styles`, `@section FullWidth` and `@section Scripts`. Render:

```cshtml
<form class="library-search-panel" method="get" action="/Library" role="search">
    <input type="hidden" name="sort" value="@Model.Sort" />
    <label for="library-search">Bạn muốn học gì?</label>
    <div class="library-search-control">
        <i class="ph ph-magnifying-glass" aria-hidden="true"></i>
        <input id="library-search" name="q" value="@Model.Search" type="search"
               placeholder="Tên bộ thẻ, mô tả hoặc tác giả..." />
        <button type="submit">Tìm bộ thẻ</button>
    </div>
</form>
```

Render summary from `Model.Summary`, sort controls with `popular`, `recent`, `cards`, cards from `Model.Items`, separate empty states for an empty library and an empty filtered result, and previous/next plus numbered page links preserving `q` and `sort`.

- [x] **Step 4: Create focused production CSS and progressive JavaScript**

Create `library.css` using the palette, typography, hero, search panel, card grid and six accent classes from prototype variant A. Include breakpoints at `900px` and `640px`, visible focus styles, `prefers-reduced-motion`, and no selectors for `.library-b`, `.library-c`, `.prototype-notice` or `.prototype-switcher`.

Create `library.js` with only the `/` shortcut:

```javascript
(() => {
    const search = document.querySelector('#library-search');
    if (!(search instanceof HTMLInputElement)) return;

    document.addEventListener('keydown', event => {
        const target = event.target;
        const isEditing = target instanceof HTMLElement &&
            (target.matches('input, textarea, select') || target.isContentEditable);
        if (event.key === '/' && !isEditing) {
            event.preventDefault();
            search.focus();
        }
    });
})();
```

- [x] **Step 5: Update navigation and homepage CTA**

Add `<a class="nav-link" href="/Library">Thư viện</a>` outside the signed-in conditional and `<li><a href="/Library">Thư viện</a></li>` in the footer. Preserve `/Set` inside the signed-in conditional. Change the home exploration CTA that currently targets the on-page public-set area to `/Library` without changing the create/register CTA.

- [x] **Step 6: Run markup and MVC tests GREEN, then commit**

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter "FullyQualifiedName~PublicLibraryMarkupTests|FullyQualifiedName~LibraryControllerTests|FullyQualifiedName~PublicLibraryRouteTests"
git add -- Views/Library/Index.cshtml wwwroot/css/library.css wwwroot/js/library.js Views/Shared/_Layout.cshtml Views/Home/Index.cshtml tests/ltwnc.Tests/Views/PublicLibraryMarkupTests.cs
git commit -m "feat(library): add public catalog experience"
```

Expected: focused tests pass and UI has no fake prototype controls/data.

---

### Task 4: Remove prototype route and complete verification

**Files:**
- Delete: `Controllers/LibraryPrototypeController.cs`
- Delete: `Views/Prototype/Library.cshtml`
- Delete: `wwwroot/css/library-prototype.css`
- Delete: `wwwroot/js/library-prototype.js`
- Modify: `tests/ltwnc.Tests/Integration/PublicLibraryRouteTests.cs`

**Interfaces:**
- Consumes: production `/Library` from Tasks 2–3.
- Produces: no reachable `/prototype/library` route and no UI prototype artefacts.

- [x] **Step 1: Add failing prototype-removal route test**

```csharp
[Fact]
public async Task OldPrototypeRoute_ReturnsNotFound()
{
    await using WebApplicationFactory<Program> factory = CreateFactory();
    using HttpClient client = CreateClient(factory);

    HttpResponseMessage response = await client.GetAsync("/prototype/library");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

- [x] **Step 2: Verify RED, remove prototype files, verify GREEN**

Run the route test and confirm it fails with `200 OK`. Delete the four prototype UI files listed above, then rerun:

```powershell
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~PublicLibraryRouteTests
```

Expected: production route returns `200`; prototype route returns `404`; `/Set` still redirects for anonymous users.

- [x] **Step 3: Run full automated verification**

```powershell
dotnet test
dotnet build --no-restore
git diff --check
rg -n "prototype/library|library-prototype|data-library-prototype|PROTOTYPE" Controllers Views wwwroot tests Program.cs
```

Expected: all tests pass, build has `0 Error(s)`, diff check is clean, and `rg` returns no prototype UI references. `Models/IPrototype.cs` remains present.

- [x] **Step 4: Run visual verification**

Start the application with `dotnet run --no-build`, open `/Library` at desktop and mobile viewport widths, and verify:

- Hero summary uses database values.
- Search submission preserves sort.
- Sort and pager links preserve `q`.
- Cards link to real `/Set/{id}` pages.
- Empty-result state is readable.
- Navigation exposes both community and personal libraries correctly.
- No horizontal overflow at 375px width.

- [x] **Step 5: Commit cleanup and any verified UI corrections**

```powershell
git add -- Controllers/LibraryPrototypeController.cs Views/Prototype/Library.cshtml wwwroot/css/library-prototype.css wwwroot/js/library-prototype.js tests/ltwnc.Tests/Integration/PublicLibraryRouteTests.cs
git commit -m "chore(library): remove catalog prototype"
```

Expected: only prototype deletion, route coverage and corrections directly required by runtime verification are included.
