# Batch Card Actions with Undo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add batch delete, star, and unstar actions to the flashcard set edit page, with persistent undo using the Command pattern.

**Architecture:** Each batch operation becomes an `ICardActionCommand` object. A central `CardActionService` runs commands and persists a `CardActionLog` snapshot. Undo rebuilds the command from the log and reverses it. Controllers stay thin, views only add checkboxes and a toolbar.

**Tech Stack:** ASP.NET Core MVC, EF Core, built-in DI, Razor Views, SQL Server.

## Global Constraints

- No new NuGet packages.
- Follow existing naming and folder conventions (`Models/Entities/`, `Services/`, `Controllers/`, `Views/FlashcardSet/`).
- Keep controllers thin; business logic lives in `CardActionService` and commands.
- Build must pass with 0 warnings after every task.
- Manual browser verification required at the end.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Models/Entities/CardActionLog.cs` | Entity that stores command metadata and undo snapshot. |
| `Models/Enums/BatchActionType.cs` | Enum for Delete, Star, Unstar. |
| `Services/CardActions/ICardActionCommand.cs` | Command contract with `ExecuteAsync` and `UndoAsync`. |
| `Services/CardActions/DeleteCardsCommand.cs` | Deletes cards and can restore them from snapshot. |
| `Services/CardActions/StarCardsCommand.cs` | Stars cards and can restore old star state. |
| `Services/CardActions/UnstarCardsCommand.cs` | Unstars cards and can restore old star state. |
| `Services/CardActionService.cs` | Runs commands, persists logs, performs undo. |
| `Controllers/CardActionsController.cs` | Receives batch action and undo requests. |
| `Views/FlashcardSet/Edit.cshtml` | Adds checkboxes, toolbar, and undo message. |
| `Data/AppDbContext.cs` | Adds `DbSet<CardActionLog>` and index. |
| `Program.cs` | Registers `CardActionService` in DI. |

---

### Task 1: Add `CardActionLog` entity, enum, and EF migration

**Files:**
- Create: `Models/Entities/CardActionLog.cs`
- Create: `Models/Enums/BatchActionType.cs`
- Modify: `Data/AppDbContext.cs`
- Test: `dotnet build` + `dotnet ef database update`

**Interfaces:**
- Produces: `CardActionLog` entity, `BatchActionType` enum, `DbSet<CardActionLog>`.

- [ ] **Step 1: Write the `BatchActionType` enum**

```csharp
namespace ltwnc.Models.Enums;

public enum BatchActionType
{
    Delete,
    Star,
    Unstar
}
```

- [ ] **Step 2: Write the `CardActionLog` entity**

```csharp
using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class CardActionLog
{
    [Key]
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int SetId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string CardIdsJson { get; set; } = string.Empty;

    public string SnapshotJson { get; set; } = string.Empty;

    public DateTime ExecutedAt { get; set; }

    public DateTime? UndoneAt { get; set; }
}
```

- [ ] **Step 3: Register the entity in `AppDbContext.cs`**

Add this property inside `AppDbContext`:

```csharp
public DbSet<CardActionLog> CardActionLogs => Set<CardActionLog>();
```

Add this configuration inside `OnModelCreating`:

```csharp
builder.Entity<CardActionLog>(entity =>
{
    entity.HasIndex(e => new { e.SetId, e.UserId, e.UndoneAt });
});
```

- [ ] **Step 4: Build and create migration**

```bash
dotnet build --configuration Release
dotnet ef migrations add AddCardActionLogs
dotnet ef database update
```

Expected: build succeeds, migration is created, database is updated.

- [ ] **Step 5: Commit**

```bash
git add Models/Entities/CardActionLog.cs Models/Enums/BatchActionType.cs Data/AppDbContext.cs Migrations/
git commit -m "feat(card-actions): add CardActionLog entity and migration"
```

---

### Task 2: Implement the command interface and concrete commands

**Files:**
- Create: `Services/CardActions/FlashcardSnapshot.cs`
- Create: `Services/CardActions/ICardActionCommand.cs`
- Create: `Services/CardActions/DeleteCardsCommand.cs`
- Create: `Services/CardActions/StarCardsCommand.cs`
- Create: `Services/CardActions/UnstarCardsCommand.cs`
- Test: `dotnet build`

**Interfaces:**
- Consumes: `AppDbContext`, `Flashcard`, `BatchActionType`.
- Produces: `ICardActionCommand`, `FlashcardSnapshot`, `DeleteCardsCommand`, `StarCardsCommand`, `UnstarCardsCommand`.

- [ ] **Step 1: Write the command interface and snapshot DTO**

`ICardActionCommand.cs`:

```csharp
namespace ltwnc.Services.CardActions;

public interface ICardActionCommand
{
    string ActionType { get; }
    int SetId { get; }
    string UserId { get; }
    IReadOnlyList<int> CardIds { get; }

    Task ExecuteAsync();
    Task UndoAsync();
}
```

`FlashcardSnapshot.cs`:

```csharp
namespace ltwnc.Services.CardActions;

public class FlashcardSnapshot
{
    public int FlashcardSetId { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string BackText { get; set; } = string.Empty;
    public string Pronunciation { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string ExampleSentence { get; set; } = string.Empty;
    public string ExampleMeaning { get; set; } = string.Empty;
    public string? Synonyms { get; set; }
    public string? ImageUrl { get; set; }
    public string? UploadedImagePath { get; set; }
    public bool IsStarred { get; set; }
    public int OrderIndex { get; set; }
}
```

- [ ] **Step 2: Write `DeleteCardsCommand`**

```csharp
using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services.CardActions;

public class DeleteCardsCommand : ICardActionCommand
{
    private readonly AppDbContext _context;
    private readonly List<FlashcardSnapshot> _snapshots = new();

    public string ActionType => "Delete";
    public int SetId { get; }
    public string UserId { get; }
    public IReadOnlyList<int> CardIds { get; }

    public DeleteCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    public async Task ExecuteAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        _snapshots.Clear();
        _snapshots.AddRange(cards.Select(c => new FlashcardSnapshot
        {
            FlashcardSetId = c.FlashcardSetId,
            FrontText = c.FrontText,
            BackText = c.BackText,
            Pronunciation = c.Pronunciation,
            PartOfSpeech = c.PartOfSpeech,
            ExampleSentence = c.ExampleSentence,
            ExampleMeaning = c.ExampleMeaning,
            Synonyms = c.Synonyms,
            ImageUrl = c.ImageUrl,
            UploadedImagePath = c.UploadedImagePath,
            IsStarred = c.IsStarred,
            OrderIndex = c.OrderIndex
        }));

        _context.Flashcards.RemoveRange(cards);
        await _context.SaveChangesAsync();
    }

    public Task UndoAsync()
    {
        foreach (var snapshot in _snapshots)
        {
            var restored = new Flashcard
            {
                FlashcardSetId = snapshot.FlashcardSetId,
                FrontText = snapshot.FrontText,
                BackText = snapshot.BackText,
                Pronunciation = snapshot.Pronunciation,
                PartOfSpeech = snapshot.PartOfSpeech,
                ExampleSentence = snapshot.ExampleSentence,
                ExampleMeaning = snapshot.ExampleMeaning,
                Synonyms = snapshot.Synonyms,
                ImageUrl = snapshot.ImageUrl,
                UploadedImagePath = snapshot.UploadedImagePath,
                IsStarred = snapshot.IsStarred,
                OrderIndex = snapshot.OrderIndex
            };
            _context.Flashcards.Add(restored);
        }

        return _context.SaveChangesAsync();
    }

    public string GetSnapshotJson()
        => JsonSerializer.Serialize(_snapshots);

    public void LoadSnapshot(string json)
    {
        _snapshots.Clear();
        var snapshots = JsonSerializer.Deserialize<List<FlashcardSnapshot>>(json) ?? new List<FlashcardSnapshot>();
        _snapshots.AddRange(snapshots);
    }
}
```

- [ ] **Step 3: Write `StarCardsCommand` and `UnstarCardsCommand`**

`StarCardsCommand.cs`:

```csharp
using System.Text.Json;
using ltwnc.Data;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

public class StarCardsCommand : ICardActionCommand
{
    private readonly AppDbContext _context;
    private readonly Dictionary<int, bool> _previousStates = new();

    public string ActionType => "Star";
    public int SetId { get; }
    public string UserId { get; }
    public IReadOnlyList<int> CardIds { get; }

    public StarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    public async Task ExecuteAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        _previousStates.Clear();
        foreach (var card in cards)
        {
            _previousStates[card.Id] = card.IsStarred;
            card.IsStarred = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UndoAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        foreach (var card in cards)
        {
            if (_previousStates.TryGetValue(card.Id, out var oldState))
            {
                card.IsStarred = oldState;
            }
        }

        await _context.SaveChangesAsync();
    }

    public string GetSnapshotJson()
        => JsonSerializer.Serialize(_previousStates);

    public void LoadSnapshot(string json)
    {
        _previousStates.Clear();
        var states = JsonSerializer.Deserialize<Dictionary<int, bool>>(json) ?? new Dictionary<int, bool>();
        foreach (var pair in states)
        {
            _previousStates[pair.Key] = pair.Value;
        }
    }
}
```

`UnstarCardsCommand.cs`:

```csharp
using System.Text.Json;
using ltwnc.Data;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

public class UnstarCardsCommand : ICardActionCommand
{
    private readonly AppDbContext _context;
    private readonly Dictionary<int, bool> _previousStates = new();

    public string ActionType => "Unstar";
    public int SetId { get; }
    public string UserId { get; }
    public IReadOnlyList<int> CardIds { get; }

    public UnstarCardsCommand(AppDbContext context, int setId, string userId, IEnumerable<int> cardIds)
    {
        _context = context;
        SetId = setId;
        UserId = userId;
        CardIds = cardIds.ToList().AsReadOnly();
    }

    public async Task ExecuteAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        _previousStates.Clear();
        foreach (var card in cards)
        {
            _previousStates[card.Id] = card.IsStarred;
            card.IsStarred = false;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UndoAsync()
    {
        var cards = await _context.Flashcards
            .Where(f => f.FlashcardSetId == SetId && CardIds.Contains(f.Id))
            .ToListAsync();

        foreach (var card in cards)
        {
            if (_previousStates.TryGetValue(card.Id, out var oldState))
            {
                card.IsStarred = oldState;
            }
        }

        await _context.SaveChangesAsync();
    }

    public string GetSnapshotJson()
        => JsonSerializer.Serialize(_previousStates);

    public void LoadSnapshot(string json)
    {
        _previousStates.Clear();
        var states = JsonSerializer.Deserialize<Dictionary<int, bool>>(json) ?? new Dictionary<int, bool>();
        foreach (var pair in states)
        {
            _previousStates[pair.Key] = pair.Value;
        }
    }
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build --configuration Release
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add Services/CardActions/
git commit -m "feat(card-actions): add ICardActionCommand and concrete commands"
```

---

### Task 3: Implement `CardActionService`

**Files:**
- Create: `Services/CardActionService.cs`
- Modify: `Program.cs`
- Test: `dotnet build`

**Interfaces:**
- Consumes: `ICardActionCommand`, `CardActionLog`, `AppDbContext`, `BatchActionType`.
- Produces: `CardActionService` with `ExecuteAsync`, `UndoAsync`, `GetUndoableLogsAsync`.

- [ ] **Step 1: Write the service**

```csharp
using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.CardActions;

public class CardActionService
{
    private readonly AppDbContext _context;

    public CardActionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CardActionLog> ExecuteAsync(ICardActionCommand command)
    {
        await command.ExecuteAsync();

        var snapshot = command switch
        {
            DeleteCardsCommand deleteCommand => deleteCommand.GetSnapshotJson(),
            StarCardsCommand starCommand => starCommand.GetSnapshotJson(),
            UnstarCardsCommand unstarCommand => unstarCommand.GetSnapshotJson(),
            _ => throw new InvalidOperationException("Unknown command type.")
        };

        var log = new CardActionLog
        {
            UserId = command.UserId,
            SetId = command.SetId,
            ActionType = command.ActionType,
            CardIdsJson = JsonSerializer.Serialize(command.CardIds),
            SnapshotJson = snapshot,
            ExecutedAt = DateTime.UtcNow
        };

        _context.CardActionLogs.Add(log);
        await _context.SaveChangesAsync();

        return log;
    }

    public async Task UndoAsync(int logId, string userId)
    {
        var log = await _context.CardActionLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);

        if (log == null)
            throw new KeyNotFoundException("Không tìm thấy hành động để hoàn tác.");

        if (log.UndoneAt.HasValue)
            throw new InvalidOperationException("Hành động này đã được hoàn tác.");

        var cardIds = JsonSerializer.Deserialize<List<int>>(log.CardIdsJson)
                      ?? new List<int>();

        ICardActionCommand command = log.ActionType switch
        {
            "Delete" => new DeleteCardsCommand(_context, log.SetId, userId, cardIds),
            "Star" => new StarCardsCommand(_context, log.SetId, userId, cardIds),
            "Unstar" => new UnstarCardsCommand(_context, log.SetId, userId, cardIds),
            _ => throw new InvalidOperationException("Unknown action type.")
        };

        // For delete, we must feed the snapshot back into the command.
        if (command is DeleteCardsCommand deleteCommand)
        {
            deleteCommand.LoadSnapshot(log.SnapshotJson);
        }
        else if (command is StarCardsCommand starCommand)
        {
            starCommand.LoadSnapshot(log.SnapshotJson);
        }
        else if (command is UnstarCardsCommand unstarCommand)
        {
            unstarCommand.LoadSnapshot(log.SnapshotJson);
        }

        await command.UndoAsync();

        log.UndoneAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CardActionLog>> GetUndoableLogsAsync(int setId, string userId, int limit = 5)
    {
        return await _context.CardActionLogs
            .Where(l => l.SetId == setId && l.UserId == userId && !l.UndoneAt.HasValue)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }
}
```

- [ ] **Step 2: Expose `Context` from `CardActionService` and add `GetLogByIdAsync`**

Add these members to `CardActionService`:

```csharp
public AppDbContext Context => _context;

public async Task<CardActionLog?> GetLogByIdAsync(int logId, string userId)
{
    return await _context.CardActionLogs
        .FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
}
```

- [ ] **Step 3: Register the service in `Program.cs`**

After `builder.Services.AddScoped<DictationService>();` add:

```csharp
builder.Services.AddScoped<CardActionService>();
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build --configuration Release
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add Services/CardActionService.cs Program.cs
git commit -m "feat(card-actions): add CardActionService and wire DI"
```

---

### Task 4: Add controller actions

**Files:**
- Create: `Controllers/CardActionsController.cs`
- Test: `dotnet build`

**Interfaces:**
- Consumes: `CardActionService`, `FlashcardSetService`, `BatchActionType`, `UserManager<IdentityUser>`.
- Produces: `CardActionsController` with `BatchAction` and `Undo` endpoints.

- [ ] **Step 1: Write the controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Enums;
using ltwnc.Services;
using ltwnc.Services.CardActions;

namespace ltwnc.Controllers;

[Authorize]
public class CardActionsController : Controller
{
    private readonly CardActionService _cardActionService;
    private readonly FlashcardSetService _setService;
    private readonly UserManager<IdentityUser> _userManager;

    public CardActionsController(
        CardActionService cardActionService,
        FlashcardSetService setService,
        UserManager<IdentityUser> userManager)
    {
        _cardActionService = cardActionService;
        _setService = setService;
        _userManager = userManager;
    }

    [HttpPost]
    [Route("/Set/{setId}/BatchAction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchAction(int setId, BatchActionType action, List<int> selectedCardIds)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var set = await _setService.GetSetByIdAsync(setId);
        if (set == null || set.UserId != user.Id)
            return Forbid();

        if (selectedCardIds == null || selectedCardIds.Count == 0)
        {
            TempData["Error"] = "Chưa chọn thẻ nào.";
            return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
        }

        try
        {
            ICardActionCommand command = action switch
            {
                BatchActionType.Delete => new DeleteCardsCommand(
                    _cardActionService.Context, setId, user.Id, selectedCardIds),
                BatchActionType.Star => new StarCardsCommand(
                    _cardActionService.Context, setId, user.Id, selectedCardIds),
                BatchActionType.Unstar => new UnstarCardsCommand(
                    _cardActionService.Context, setId, user.Id, selectedCardIds),
                _ => throw new InvalidOperationException("Hành động không hợp lệ.")
            };

            var log = await _cardActionService.ExecuteAsync(command);
            TempData["Success"] = $"Đã {Describe(action)} {selectedCardIds.Count} thẻ. ";
            TempData["UndoLogId"] = log.Id;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Edit", "FlashcardSet", new { id = setId });
    }

    [HttpPost]
    [Route("/CardActions/Undo/{logId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(int logId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await _cardActionService.UndoAsync(logId, user.Id);
            TempData["Success"] = "Đã hoàn tác hành động.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        // We need the set id to redirect; fetch it from the log.
        var log = await _cardActionService.GetLogByIdAsync(logId, user.Id);
        if (log == null)
            return RedirectToAction("Index", "FlashcardSet");

        return RedirectToAction("Edit", "FlashcardSet", new { id = log.SetId });
    }

    private static string Describe(BatchActionType action) => action switch
    {
        BatchActionType.Delete => "xóa",
        BatchActionType.Star => "đánh sao",
        BatchActionType.Unstar => "bỏ sao",
        _ => "thực hiện"
    };
}
```

- [ ] **Step 2: Expose `Context` from `CardActionService` or inject `AppDbContext` directly**

The plan above uses `_cardActionService.Context`. Add this property to `CardActionService`:

```csharp
public AppDbContext Context => _context;
```

Also add `GetLogByIdAsync`:

```csharp
public async Task<CardActionLog?> GetLogByIdAsync(int logId, string userId)
{
    return await _context.CardActionLogs
        .FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build --configuration Release
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add Controllers/CardActionsController.cs Services/CardActionService.cs
git commit -m "feat(card-actions): add CardActionsController endpoints"
```

---

### Task 5: Update the edit page UI

**Files:**
- Modify: `Views/FlashcardSet/Edit.cshtml`
- Test: `dotnet build` + manual browser check

**Interfaces:**
- Consumes: `BatchActionType`, `TempData["UndoLogId"]`, `TempData["Success"]`, `TempData["Error"]`.
- Produces: HTML form with checkboxes, toolbar, undo message.

- [ ] **Step 1: Wrap the card list in a form and add checkboxes**

After the existing `@if (!cards.Any())` block and before the closing `</aside>`, replace the card list loop with a form that submits to `/Set/{Model.Id}/BatchAction`:

```razor
<form asp-controller="CardActions" asp-action="BatchAction" asp-route-setId="@Model.Id" method="post" id="batch-form">
    @Html.AntiForgeryToken()
    <div class="batch-toolbar mb-3">
        <button type="submit" name="action" value="Delete" class="btn-secondary-custom" onclick="return confirm('Xóa các thẻ đã chọn?');">
            <i class="ph ph-trash"></i> Xóa đã chọn
        </button>
        <button type="submit" name="action" value="Star" class="btn-secondary-custom">
            <i class="ph ph-star"></i> Đánh sao
        </button>
        <button type="submit" name="action" value="Unstar" class="btn-secondary-custom">
            <i class="ph ph-star-off"></i> Bỏ sao
        </button>
    </div>

    @foreach (var card in cards.OrderBy(c => c.OrderIndex))
    {
        <div class="vocab-list-item-wrapper">
            <input type="checkbox" name="selectedCardIds" value="@card.Id" class="card-checkbox" />
            <button type="button"
                    class="vocab-list-item @(card == cards.OrderBy(c => c.OrderIndex).First() ? "is-active" : "")"
                    data-card-id="@card.Id">
                <span class="vocab-star">@(card.IsStarred ? "★" : "☆")</span>
                <span class="vocab-list-copy">
                    <strong>@card.FrontText</strong>
                    <small>@card.PartOfSpeech · @card.Pronunciation</small>
                    <span>@card.BackText</span>
                </span>
            </button>
        </div>
    }
</form>
```

- [ ] **Step 2: Add the undo message after the existing error alert**

After the existing `TempData["Error"]` block, add:

```razor
@if (TempData["Success"] is string success)
{
    <div class="alert alert-success d-flex justify-content-between align-items-center">
        <span>@success</span>
        @if (TempData["UndoLogId"] is int undoLogId)
        {
            <form asp-controller="CardActions" asp-action="Undo" asp-route-logId="@undoLogId" method="post" class="m-0">
                @Html.AntiForgeryToken()
                <button type="submit" class="btn btn-sm btn-link">Hoàn tác</button>
            </form>
        }
    </div>
}
```

- [ ] **Step 3: Add minimal CSS to keep the layout usable**

In `wwwroot/css/edit.css`, add:

```css
.batch-toolbar {
    display: flex;
    gap: 0.5rem;
    flex-wrap: wrap;
}

.vocab-list-item-wrapper {
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

.card-checkbox {
    width: 1.25rem;
    height: 1.25rem;
    flex-shrink: 0;
}
```

If `edit.css` does not exist, create it and include it in `_Layout.cshtml` or reference it from `Edit.cshtml` (it already is).

- [ ] **Step 4: Build to verify**

```bash
dotnet build --configuration Release
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add Views/FlashcardSet/Edit.cshtml wwwroot/css/edit.css
git commit -m "feat(card-actions): add batch action UI and undo link"
```

---

### Task 6: Verify end-to-end

**Files:**
- None
- Test: manual browser verification

- [ ] **Step 1: Start the app**

```bash
dotnet run --configuration Release --urls "http://localhost:5000"
```

- [ ] **Step 2: Log in and open a set with cards**

Go to `/Set/{id}/Edit` for a set you own.

- [ ] **Step 3: Test batch delete with undo**

- Select 2 cards.
- Click "Xóa đã chọn".
- Verify the cards disappear and the success message shows "Đã xóa 2 thẻ. Hoàn tác".
- Click "Hoàn tác".
- Verify the cards reappear.
- Refresh the page and click "Hoàn tác" again. Verify it still works.

- [ ] **Step 4: Test batch star/unstar**

- Select 2 unstarred cards and click "Đánh sao".
- Verify they show ★.
- Click "Hoàn tác" and verify they return to ☆.
- Repeat with "Bỏ sao".

- [ ] **Step 5: Test edge cases**

- Submit batch action with no cards selected. Verify error message "Chưa chọn thẻ nào.".
- Undo the same action twice. Verify second undo shows error.

- [ ] **Step 6: Commit verification notes (optional)**

If all checks pass, no code change is needed.

---

## Spec Coverage

| Spec Requirement | Task |
|------------------|------|
| `CardActionLog` entity with snapshot | Task 1 |
| `BatchActionType` enum | Task 1 |
| `ICardActionCommand` contract | Task 2 |
| `DeleteCardsCommand` with snapshot/undo | Task 2 |
| `StarCardsCommand` / `UnstarCardsCommand` | Task 2 |
| `CardActionService` runs commands and undo | Task 3 |
| Controller endpoints for batch and undo | Task 4 |
| Edit page checkboxes, toolbar, undo link | Task 5 |
| Manual end-to-end verification | Task 6 |

## Placeholder Scan

- No TBD/TODO/fill-in-details.
- All code blocks contain complete, copy-pasteable C# / Razor / CSS.
- All file paths are exact.
- Type names match across tasks (`CardActionLog`, `BatchActionType`, `CardActionService`, `ICardActionCommand`).

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-07-11-batch-card-actions-plan.md`. Two execution options:**

1. **Subagent-Driven (recommended)** - dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints.

**Which approach?**
