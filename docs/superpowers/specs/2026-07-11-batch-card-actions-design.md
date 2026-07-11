# Batch Card Actions with Undo

**Date:** 2026-07-11
**Status:** Approved
**Scope:** Add batch delete, star, and unstar actions to the flashcard set edit page, using the Command pattern to support undo across page refreshes.

## Problem

The edit page (`/Set/{id}/Edit`) only allows editing or deleting cards one by one. Users often want to clean up a deck quickly: delete many cards, star many cards, or remove stars from many cards at once. Without batch actions, this is slow and repetitive.

A single mistake in a batch operation (for example, deleting the wrong cards) is also costly because there is no undo. A direct service call deletes the card permanently.

## Goal

Add batch actions to the set edit page and make them undoable, even after the page refreshes. Use the Command pattern so each batch operation is encapsulated as an object with a clear `Execute` and `Undo` path.

Keep the first version small: only delete, star, and unstar. Move and tag can be added later without changing the command infrastructure.

## Design

### User-facing behavior

On `/Set/{id}/Edit`:

- Each card row has a checkbox.
- A toolbar above or below the card list shows batch actions:
  - "Xóa đã chọn"
  - "Đánh sao đã chọn"
  - "Bỏ sao đã chọn"
- After a batch action runs, the page redirects back with a message like:
  - "Đã xóa 3 thẻ. [Hoàn tác]"
  - "Đã đánh sao 5 thẻ. [Hoàn tác]"
- "Hoàn tác" restores the previous state, even if the user refreshes the page first.

### Architecture

```text
Views/FlashcardSet/Edit.cshtml
   |
   | POST /Set/{setId}/BatchAction
   v
FlashcardSetController.BatchAction
   |
   | creates ICardActionCommand
   v
CardActionService.ExecuteAsync(command)
   |
   | runs Execute + saves CardActionLog
   v
ICardActionCommand  ---->  DeleteCardsCommand
                    |----> StarCardsCommand
                    |----> UnstarCardsCommand
   |
   | writes/reads AppDbContext
   v
SQL Server
```

Undo path:

```text
POST /CardActions/Undo/{logId}
   |
   v
CardActionService.UndoAsync(logId)
   |
   | loads CardActionLog, rebuilds command
   v
command.UndoAsync()
```

### Command interface

```csharp
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

Each command owns the actual data change and knows how to reverse it.

### Commands

#### DeleteCardsCommand

- `ExecuteAsync`: delete the selected cards. Before deleting, capture a snapshot of each card and store it in the command context.
- `UndoAsync`: re-insert the deleted cards with their original data, including `OrderIndex`.

#### StarCardsCommand

- `ExecuteAsync`: set `IsStarred = true` on selected cards.
- `UndoAsync`: restore each card's previous `IsStarred` value.

#### UnstarCardsCommand

- `ExecuteAsync`: set `IsStarred = false` on selected cards.
- `UndoAsync`: restore each card's previous `IsStarred` value.

### Persistence

Add a `CardActionLogs` table:

| Column | Type | Purpose |
| --- | --- | --- |
| Id | int | Primary key |
| UserId | string | Who performed the action |
| SetId | int | Which set the action belongs to |
| ActionType | string | Delete, Star, or Unstar |
| CardIdsJson | string | JSON array of affected card ids |
| SnapshotJson | string | JSON snapshot needed for undo |
| ExecutedAt | DateTime | When the action ran |

`SnapshotJson` content depends on `ActionType`:

- `Delete`: JSON array of full `Flashcard` objects before deletion.
- `Star` / `Unstar`: dictionary of `{ cardId: previousIsStarred }`.

This log is the persistent command history. It enables undo after refresh and can later support an audit trail.

### Services

`CardActionService` is the central invoker:

```csharp
public class CardActionService
{
    public Task<CardActionLog> ExecuteAsync(ICardActionCommand command);
    public Task UndoAsync(int logId);
    public Task<IReadOnlyList<CardActionLog>> GetRecentActionsAsync(int setId, string userId, int limit = 10);
}
```

`ExecuteAsync`:
1. Calls `command.ExecuteAsync()`.
2. Serializes the command metadata and snapshot into a `CardActionLog`.
3. Saves the log to the database.

`UndoAsync`:
1. Loads the log by id, verifying ownership (same user, same set).
2. Rebuilds the correct command from the log data.
3. Calls `command.UndoAsync()`.
4. Marks the log as undone or deletes it to prevent double undo.

### Controller changes

Add to `FlashcardSetController`:

```csharp
[HttpPost]
[Route("/Set/{setId}/BatchAction")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> BatchAction(
    int setId,
    BatchActionType action,
    List<int> selectedCardIds)
```

Add a separate controller or action for undo:

```csharp
[HttpPost]
[Route("/CardActions/Undo/{logId}")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Undo(int logId)
```

### View changes

Modify `Views/FlashcardSet/Edit.cshtml`:

- Add a checkbox on each card row.
- Add a batch action toolbar.
- Show a temporary undo message after a batch action via `TempData`.

### Error handling

- Invalid card ids or cards not owned by the user are skipped or cause the entire batch to fail, depending on the command implementation. The first version should fail fast with a clear message.
- If a log cannot be found for undo, return `NotFound`.
- If undo cannot complete because the underlying data changed (for example, a card was re-deleted by another action), return an error message and do not leave the database in a partial state.

### Testing

- Run `dotnet build --configuration Release` with no warnings.
- Manual verification:
  1. Open `/Set/{id}/Edit`.
  2. Select multiple cards and delete them.
  3. Click "Hoàn tác" and verify cards reappear.
  4. Refresh the page, click "Hoàn tác" again, and verify it still works.
  5. Repeat for star and unstar actions.

## Acceptance criteria

- [ ] `/Set/{id}/Edit` shows checkboxes and a batch action toolbar.
- [ ] User can delete multiple cards in one action.
- [ ] User can star multiple cards in one action.
- [ ] User can unstar multiple cards in one action.
- [ ] Each batch action creates a `CardActionLog` entry.
- [ ] Undo restores the previous state, even after page refresh.
- [ ] A log cannot be undone twice.
- [ ] Build passes with no warnings.

## Out of scope

- Batch move cards to another set.
- Batch add/remove tags.
- Audit UI or full action history page.
- Undo/redo stack with multiple levels.

## Files changed

- Create `Models/Entities/CardActionLog.cs`
- Create `Services/CardActionService.cs`
- Create `Services/CardActions/ICardActionCommand.cs`
- Create `Services/CardActions/DeleteCardsCommand.cs`
- Create `Services/CardActions/StarCardsCommand.cs`
- Create `Services/CardActions/UnstarCardsCommand.cs`
- Create `Models/ViewModels/FlashcardSet/BatchActionRequest.cs` or use action parameters
- Modify `Controllers/FlashcardSetController.cs`
- Modify `Views/FlashcardSet/Edit.cshtml`
- Modify `Data/AppDbContext.cs`
- Add EF Core migration for `CardActionLogs`
- Modify `Program.cs` to register `CardActionService`
