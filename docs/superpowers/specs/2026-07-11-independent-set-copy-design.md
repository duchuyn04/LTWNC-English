# Independent Public Set Copy Design

## Goal

Let a signed-in learner copy a public flashcard set into a privately owned, independent set before studying it. The source author can then edit or delete their original set without affecting the learner's cards, progress, or dictation history.

## Problem

Study currently accepts any accessible public set. A learner's `UserProgress` and `DictationSessionDetail` rows therefore reference the original author's `Flashcard` IDs. Deleting those cards couples the author's batch delete and undo workflow to every learner's history.

## Product Decision

Public sets are templates, not shared live study data.

1. A visitor can view a public set.
2. A signed-in non-owner clicks **Sao chép và học**.
3. The application creates a private copy owned by that learner, with new set and card IDs.
4. The learner is redirected to the copied set’s study page.
5. The learner studies and edits only their copy. Progress and dictation history reference only the copied cards.

No background synchronization is included. If an author later changes a public template, existing copies do not change. Versioning, copy updates, and conflict resolution are out of scope.

## Data Model

Add nullable `SourceSetId` to `FlashcardSet`.

- It stores the numeric ID of the public template from which a copy was made.
- It intentionally has no database foreign key. Deleting the source must not block, modify, or invalidate the copy.
- A unique index on `(UserId, SourceSetId)` applies only when `SourceSetId` is non-null, preventing duplicate copies of the same public template for one learner.
- Original sets keep `SourceSetId = null`.

The copy operation creates a new `FlashcardSet` with:

```csharp
new FlashcardSet
{
    Title = source.Title,
    Description = source.Description,
    UserId = learnerId,
    IsPublic = false,
    SourceSetId = source.Id,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
}
```

Each source `Flashcard` is cloned with new identity values and the same editable study content: text, pronunciation, part of speech, examples, synonyms, `ImageUrl`, `UploadedImagePath`, starred state, and order. Copying preserves current image references; it does not duplicate files.

`UserProgress`, `StudySession`, and `DictationSessionDetail` are never copied. The learner starts a new progress history for the copied cards.

## Service Boundary

`FlashcardSetService` owns copying through:

```csharp
Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId);
```

The method loads the source set with cards, validates `IsPublic` and `source.UserId != learnerId`, then either:

- returns the learner's existing copy for `(learnerId, sourceSetId)`, or
- creates the set and all cards in one EF Core transaction.

The controller does not clone entities or query cards directly.

## HTTP and UI

Add an authenticated antiforgery-protected endpoint:

```text
POST /Set/{id}/Copy
```

For a public set viewed by a non-owner, `Views/FlashcardSet/Details.cshtml` renders a **Sao chép và học** submit form instead of a direct `/Study/{id}` link. The endpoint redirects to `/Study/{copiedSetId}` and sets a success message.

If the learner already owns a copy, the public detail page renders **Mở bộ đã sao chép** linking to that copy. The POST endpoint is idempotent as a race-safe fallback and redirects to the same existing copy.

Public-set study routes are no longer a supported learner flow. `StudyController` must allow study only when `FlashcardSet.UserId == currentUser.Id`. Anonymous visitors and non-owners are redirected to the public set details page, where they can copy after signing in. This prevents direct URL entry from recreating shared progress.

## Command Pattern Follow-up

The action system remains a Command pattern, but each command must own its persistence payload so `CardActionService` never switches on concrete command classes.

Extend `ICardActionCommand` with:

```csharp
string GetSnapshotJson();
void LoadSnapshot(string json);
```

`CardActionService.ExecuteAsync` calls `command.GetSnapshotJson()`. Undo resolves commands by `ActionType` through one factory, calls `LoadSnapshot`, and then calls `UndoAsync`.

For delete undo, extend `FlashcardSnapshot` with `UserProgressSnapshot` rows. The delete command removes dependent progress, dictation details, and cards in the existing transaction; undo restores all three with their original identities. This makes undo an exact reversal for the owner’s private set.

## Security and Error Behavior

- Only authenticated users can copy.
- A private set cannot be copied by a non-owner.
- An owner cannot copy their own set.
- Copy uses an EF transaction and the unique index to prevent duplicate copies during concurrent requests.
- A direct study request for a set the user does not own cannot create progress or study history.

## Verification

1. A learner copies a public set and receives a private set with distinct card IDs.
2. Copying the same set twice opens the existing copy; only one copy exists.
3. Editing/deleting the source set leaves the copied set and its study history usable.
4. A non-owner cannot study a public set through `/Study/{sourceSetId}`.
5. Batch delete and undo on a copied set restore cards, user progress, and dictation details.
6. Release build completes with 0 warnings.
