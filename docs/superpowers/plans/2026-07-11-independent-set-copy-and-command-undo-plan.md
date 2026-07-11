# Independent Set Copy and Command Undo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Copy public sets into learner-owned libraries before study, and make delete undo restore every dependent record without concrete-command switches in the invoker.

**Architecture:** A copy gets a new private set and new card IDs, so source edits/deletes never touch learner history. Commands serialize their own snapshots; a factory rebuilds them from persisted action types. Delete snapshots contain cards, progress, and dictation details, and each operation is transactional.

**Tech Stack:** ASP.NET Core MVC, EF Core 10, SQL Server, Razor, xUnit with EF Core InMemory/SQLite test providers.

## Global Constraints

- No runtime NuGet packages; test-only packages live under `tests/`.
- A learner must own a set before any study route creates progress or history.
- Each learner has at most one copy of one source set.
- Source deletion must not affect a copy.
- Delete undo restores cards, `UserProgress`, and `DictationSessionDetails` with their original IDs.
- Build and tests complete with 0 warnings.
- Browser verification uses MCP Playwright through the in-app browser skill. Sign in with the test account supplied by the user at execution time; do not store its password in this plan or source control.

---

## File Structure

| File | Responsibility |
|---|---|
| `Models/Entities/FlashcardSet.cs` | Optional source lineage. |
| `Data/AppDbContext.cs`, `Migrations/*` | Filtered unique copy index and schema. |
| `Services/FlashcardSetService.cs` | Idempotent deep-copy operation. |
| `Controllers/FlashcardSetController.cs` | Copy endpoint and details view model. |
| `Controllers/StudyController.cs` | Ownership-only study access. |
| `Views/FlashcardSet/Details.cshtml` | Copy/open-copy UX. |
| `Services/CardActions/*` | Command snapshot contract, factory, exact delete undo. |
| `tests/ltwnc.Tests/*` | Copy, factory, and delete/undo tests. |

### Task 1: Create the test project and copy lineage migration

**Files:**
- Modify: `ltwnc.csproj`, `Models/Entities/FlashcardSet.cs`, `Data/AppDbContext.cs`
- Create: `tests/ltwnc.Tests/ltwnc.Tests.csproj`, `tests/ltwnc.Tests/FlashcardSetCopyTests.cs`, `Migrations/<timestamp>_AddSourceSetIdToFlashcardSets.cs`
- Test: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj`

**Interfaces:** `FlashcardSet.SourceSetId : int?`; unique filtered index `(UserId, SourceSetId)`.

- [ ] **Step 1: Create test dependencies**

Create `tests/ltwnc.Tests/ltwnc.Tests.csproj` referencing `../../ltwnc.csproj`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.EntityFrameworkCore.InMemory` version `10.0.9`, and `Microsoft.EntityFrameworkCore.Sqlite` version `10.0.9`.

- [ ] **Step 2: Write the failing lineage test**

```csharp
[Fact]
public async Task CopyPublicSetAsync_returns_the_existing_copy_for_the_same_learner()
{
    var source = await SeedPublicSetAsync("author", "Public");
    var first = await _service.CopyPublicSetAsync(source.Id, "learner");
    var second = await _service.CopyPublicSetAsync(source.Id, "learner");

    Assert.Equal(first.Id, second.Id);
    Assert.False(first.IsPublic);
    Assert.Equal(source.Id, first.SourceSetId);
}
```

- [ ] **Step 3: Run the test**

Run: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~CopyPublicSetAsync_returns_the_existing_copy_for_the_same_learner`

Expected: compile failure because the model and service API do not exist.

- [ ] **Step 4: Add the schema**

Add:

```csharp
public int? SourceSetId { get; set; }
```

Configure:

```csharp
entity.HasIndex(e => new { e.UserId, e.SourceSetId })
    .IsUnique()
    .HasFilter("[SourceSetId] IS NOT NULL");
```

Generate `AddSourceSetIdToFlashcardSets`; do not add a foreign key.

- [ ] **Step 5: Verify and commit**

```bash
dotnet build --configuration Release
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
git add Models/Entities/FlashcardSet.cs Data/AppDbContext.cs Migrations/ ltwnc.csproj tests/
git commit -m "feat(sets): add source lineage for copied sets"
```

### Task 2: Implement idempotent public-set copying

**Files:**
- Modify: `Services/FlashcardSetService.cs`, `tests/ltwnc.Tests/FlashcardSetCopyTests.cs`
- Test: `dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~CopyPublicSetAsync`

**Interfaces:** `Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId)`.

- [ ] **Step 1: Add the failing deep-copy test**

```csharp
[Fact]
public async Task CopyPublicSetAsync_creates_private_cards_with_new_ids()
{
    var source = await SeedPublicSetAsync("author", "Public", cardCount: 2);
    var copy = await _service.CopyPublicSetAsync(source.Id, "learner");
    var copied = await _context.Flashcards.Where(c => c.FlashcardSetId == copy.Id).ToListAsync();

    Assert.False(copy.IsPublic);
    Assert.Equal(2, copied.Count);
    Assert.DoesNotContain(copied, c => source.Flashcards.Any(sourceCard => sourceCard.Id == c.Id));
}
```

- [ ] **Step 2: Implement the service method**

Load source cards. Reject a missing/private source and the source owner. Return the existing learner copy when present. Otherwise, in `BeginTransactionAsync`, create:

```csharp
var copy = new FlashcardSet
{
    Title = source.Title,
    Description = source.Description,
    UserId = learnerId,
    IsPublic = false,
    SourceSetId = source.Id,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

Clone every editable card field: text, examples, pronunciation, synonyms, image references, star state, and `OrderIndex`. Do not copy progress, sessions, or dictation details.

- [ ] **Step 3: Make concurrent copies idempotent**

On `DbUpdateException`, clear failed tracked entries, query `UserId == learnerId && SourceSetId == sourceSetId`, return that copy if found, otherwise rethrow.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~CopyPublicSetAsync
git add Services/FlashcardSetService.cs tests/ltwnc.Tests/FlashcardSetCopyTests.cs
git commit -m "feat(sets): copy public templates into private libraries"
```

- [ ] **Step 5: Check copy logic through the application boundary**

Start the app, sign in with the user-supplied test account through MCP Playwright, and use an existing public set owned by another account as the source. Submit the copy action twice. Verify the first response redirects to a learner-owned `/Study/{copySetId}` and the second opens that same copy; in the database, assert exactly one `FlashcardSet` for the learner has `SourceSetId == sourceSetId` and that every copied card ID differs from the source card IDs.

### Task 3: Require a private copy before studying

**Files:**
- Modify: `Controllers/FlashcardSetController.cs`, `Controllers/StudyController.cs`, `Models/ViewModels/FlashcardSet/SetDetailViewModel.cs`, `Views/FlashcardSet/Details.cshtml`, `Services/FlashcardSetService.cs`
- Test: browser check

**Interfaces:** `SetDetailViewModel.ExistingCopyId : int?`; `POST /Set/{id}/Copy`; `Task<FlashcardSet?> GetOwnedSetAsync(int id, string userId)`.

- [ ] **Step 1: Populate copy state on the public detail page**

For an authenticated non-owner viewing a public set, query their copy and assign `ExistingCopyId`.

- [ ] **Step 2: Add the POST endpoint**

```csharp
[HttpPost]
[Route("/Set/{id}/Copy")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Copy(int id)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    var copy = await _setService.CopyPublicSetAsync(id, user.Id);
    TempData["Success"] = "Đã sao chép bộ thẻ vào thư viện của bạn.";
    return RedirectToAction("Index", "Study", new { setId = copy.Id });
}
```

Map missing sources to `NotFound` and invalid ownership to `Forbid`.

- [ ] **Step 3: Replace the public study CTA**

Keep the owner’s `/Study/{id}` link. For a signed-in non-owner, render an antiforgery **Sao chép và học** form; render **Mở bộ đã sao chép** when `ExistingCopyId` is present. Anonymous visitors see a login link.

- [ ] **Step 4: Close direct-study access**

Implement `GetOwnedSetAsync`. Replace every `GetAccessibleSetAsync` use in `StudyController` with this ownership check. A non-owner redirects to `FlashcardSetController.Details`, so direct URLs cannot create shared progress.

- [ ] **Step 5: Verify and commit**

As B, copy A’s public set, study B’s copy, then edit/delete A’s source. Confirm B’s set still opens and retains progress.

```bash
dotnet build --configuration Release
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
git add Controllers/FlashcardSetController.cs Controllers/StudyController.cs Models/ViewModels/FlashcardSet/SetDetailViewModel.cs Views/FlashcardSet/Details.cshtml Services/FlashcardSetService.cs tests/
git commit -m "feat(sets): require private copies before study"
```

- [ ] **Step 6: Run responsive UI/UX checks with MCP Playwright**

After signing in with the supplied test account, use a public set not owned by that account and verify these visible states:

1. At `1440x900`, the public detail page shows one actionable **Sao chép và học** control and no direct study control for the source.
2. Submit the copy form and verify the success message and redirect to the private copy’s study page.
3. Reload the source detail page; verify **Mở bộ đã sao chép** replaces the copy form.
4. At `390x844`, verify the CTA remains visible and usable, the cards do not horizontally overflow, and the page reports `document.documentElement.scrollWidth <= document.documentElement.clientWidth`.
5. Navigate directly to `/Study/{sourceSetId}` and verify the browser returns to the source detail page rather than a study screen.

### Task 4: Let commands own snapshots

**Files:**
- Modify: `Services/CardActions/ICardActionCommand.cs`, `DeleteCardsCommand.cs`, `StarCardsCommand.cs`, `UnstarCardsCommand.cs`, `Services/CardActionService.cs`, `Program.cs`
- Create: `Services/CardActions/CardActionCommandFactory.cs`, `tests/ltwnc.Tests/CardActionCommandFactoryTests.cs`
- Test: factory tests

**Interfaces:** add `string GetSnapshotJson()` and `void LoadSnapshot(string json)` to `ICardActionCommand`; factory `Create(string actionType, int setId, string userId, IReadOnlyList<int> cardIds)`.

- [ ] **Step 1: Write the failing factory test**

```csharp
[Theory]
[InlineData("Delete", typeof(DeleteCardsCommand))]
[InlineData("Star", typeof(StarCardsCommand))]
[InlineData("Unstar", typeof(UnstarCardsCommand))]
public void Create_returns_the_matching_command(string actionType, Type expectedType)
    => Assert.IsType(expectedType, _factory.Create(actionType, 1, "user", [1]));
```

- [ ] **Step 2: Implement the factory and interface**

Move each existing command’s snapshot methods into the interface contract. The factory receives `AppDbContext` and contains the only action-type switch; unknown types throw `InvalidOperationException`.

- [ ] **Step 3: Remove concrete switches from the invoker**

In `ExecuteAsync`, replace snapshot selection with:

```csharp
var snapshot = command.GetSnapshotJson();
```

In `UndoAsync`, create via the factory, then:

```csharp
command.LoadSnapshot(log.SnapshotJson);
await command.UndoAsync();
```

Register the scoped factory and inject it into `CardActionService`.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~CardActionCommandFactoryTests
dotnet build --configuration Release
git add Services/CardActions/ Services/CardActionService.cs Program.cs tests/
git commit -m "refactor(card-actions): let commands own snapshots"
```

### Task 5: Make delete undo restore progress exactly

**Files:**
- Modify: `Services/CardActions/FlashcardSnapshot.cs`, `Services/CardActions/DeleteCardsCommand.cs`
- Create: `Services/CardActions/UserProgressSnapshot.cs`, `tests/ltwnc.Tests/DeleteCardsCommandTests.cs`
- Test: SQLite round trip

**Interfaces:** `FlashcardSnapshot.UserProgresses : List<UserProgressSnapshot>`; snapshot fields are `Id`, `UserId`, `FlashcardId`, `IsLearned`, `Status`, `CorrectCount`, `WrongCount`, `LastReviewed`.

- [ ] **Step 1: Write the failing round-trip test**

```csharp
[Fact]
public async Task UndoAsync_restores_cards_progress_and_dictation_details()
{
    var command = new DeleteCardsCommand(_context, _set.Id, _owner.Id, [_card.Id]);
    await command.ExecuteAsync();
    var undo = new DeleteCardsCommand(_context, _set.Id, _owner.Id, [_card.Id]);
    undo.LoadSnapshot(command.GetSnapshotJson());

    await undo.UndoAsync();

    Assert.NotNull(await _context.Flashcards.FindAsync(_card.Id));
    Assert.NotNull(await _context.UserProgresses.FindAsync(_progress.Id));
    Assert.NotNull(await _context.DictationSessionDetails.FindAsync(_detail.Id));
}
```

Use one open SQLite in-memory connection; it enforces the foreign keys relevant to this flow.

- [ ] **Step 2: Snapshot and delete progress together**

Load progress rows before deletion and attach them to their card snapshots. Remove tracked progress, dictation details, and cards in the same unit of work; remove the separate `ExecuteDeleteAsync` call.

- [ ] **Step 3: Restore in FK order**

Restore cards with the existing `IDENTITY_INSERT [Flashcards]` helper. Restore `UserProgresses` next with an equivalent `IDENTITY_INSERT [UserProgresses]` helper, then restore dictation details. All remains inside the existing `CardActionService` transaction.

- [ ] **Step 4: Verify and commit**

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --filter FullyQualifiedName~DeleteCardsCommandTests
dotnet build --configuration Release
git add Services/CardActions/ tests/
git commit -m "fix(card-actions): restore progress with delete undo"
```

- [ ] **Step 5: Run batch-action UI regression checks with MCP Playwright**

On the learner-owned copy at `1440x900` and `390x844`, select cards, run Star, Unstar, Delete, and Undo. For each action, assert the success/undo affordance is visible, the expected star/card state changes, and no horizontal overflow occurs. After delete undo, refresh the page and verify the restored card remains selectable and its study progress and dictation result still appear.

### Task 6: End-to-end verification

**Files:** None

- [ ] **Step 1: Apply schema**

```bash
dotnet ef database update
```

- [ ] **Step 2: Verify copy isolation**

A copies a public set; B copies it twice; confirm one private B copy with new card IDs. B studies it. A edits and deletes the source; B’s copy, progress, and dictation results remain usable.

- [ ] **Step 3: Verify exact command undo**

On B’s copied set, create progress and a dictation answer; batch-delete the card, undo, refresh, and confirm the card, counters, and dictation record return.

- [ ] **Step 4: Run final checks**

```bash
dotnet build --configuration Release
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj
```

Expected: all tests pass; build reports `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 5: Capture final Playwright evidence**

With the supplied test account logged in, use MCP Playwright to capture one desktop (`1440x900`) and one mobile (`390x844`) screenshot of the copied set detail page and the batch-action edit page. Verify each screenshot shows a readable CTA/toolbar, no clipped controls, and no horizontal overflow. Do not persist credentials in screenshots, logs, or the plan.

## Spec Coverage

| Requirement | Tasks |
|---|---|
| Independent learner-owned copies | 1–3 |
| No non-owner direct study | 3 |
| Command-owned snapshots | 4 |
| Exact progress-aware delete undo | 5 |
| Migration and browser verification | 1, 6 |

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-11-independent-set-copy-and-command-undo-plan.md`. Two execution options:

1. Subagent-Driven (recommended) - I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. Inline Execution - Execute tasks in this session using executing-plans, batch execution with checkpoints.
