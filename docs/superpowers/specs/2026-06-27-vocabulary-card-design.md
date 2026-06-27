# Vocabulary Card Fields Design

## Goal

Extend the existing flashcard model so each card can store richer vocabulary data like the provided reference image, while keeping the current flashcard study flow compatible.

The feature adds:

- More vocabulary fields on each existing `Flashcard`.
- A polished vocabulary editor UI inside the existing set edit page.
- Starred cards that can be studied separately from all cards.

This design intentionally keeps the existing `Flashcard` entity and does not introduce a separate vocabulary model.

## Decisions

### Data Model

Extend `Flashcard` with these fields:

| Field | Meaning | Required |
| --- | --- | --- |
| `FrontText` | Thuật ngữ | Yes |
| `BackText` | Định nghĩa | Yes |
| `Pronunciation` | IPA/phát âm | Yes |
| `PartOfSpeech` | Loại từ | Yes |
| `ExampleSentence` | Ví dụ tiếng Anh | Yes |
| `ExampleMeaning` | Nghĩa câu ví dụ tiếng Việt | Yes |
| `Synonyms` | Từ đồng nghĩa | No |
| `IsStarred` | Đánh dấu sao | No, default false |

The database migration will add these columns to `Flashcards`.

Existing rows need database-safe defaults for newly required text columns so migration succeeds. Application validation will enforce the required fields for all new and edited cards after this feature ships.

### UI Model

Use the approved visual direction from mockup v2:

- Left side: compact vocabulary list for fast scanning.
- Right side: focused editor panel for the selected card.
- Header actions include regular study and starred study.
- The current selected card shows all editable fields.
- Star state is visible in the list and editable in the detail panel.

The set edit page remains the home for managing cards. The UI will use Razor, existing CSS patterns, and light page-specific CSS where needed. No new frontend framework will be introduced.

`PartOfSpeech` uses an HTML `datalist` with common options while still allowing custom values:

- noun
- verb
- adjective
- adverb
- phrase
- idiom
- other

### Add and Edit Behavior

The existing add/edit card actions will be expanded to accept the new fields.

Validation rules:

- Required: `FrontText`, `BackText`, `Pronunciation`, `PartOfSpeech`, `ExampleSentence`, `ExampleMeaning`.
- Optional: `Synonyms`.
- Text inputs are trimmed before save.
- Empty required values return the user to the current set edit page with a clear error message.

Authorization stays unchanged for editing:

- Only the set owner can add, edit, delete, or star cards.

### Study Behavior

The default flashcard study mode remains unchanged:

- Front side: `FrontText`.
- Back side: `BackText`.

The additional vocabulary fields are for editing/detail display only in this scope.

Add starred study support:

- `/Study/{setId}` shows a new action: "Học thẻ đã sao".
- `/Study/{setId}/Flashcard?starredOnly=true` studies only starred cards.
- The flashcard screen provides a small toggle between all cards and starred cards.
- If no starred cards exist, the app shows a message and redirects back to the study mode page.

Study access control remains:

- Users can study public sets.
- Users can study their own private sets.
- Users cannot study another user's private set.

## Architecture

Follow the existing Controller -> Service -> Repository pattern.

### Entity and Migration

- Update `Models/Entities/Flashcard.cs`.
- Add EF migration for the new columns.

### Repository

Update `IFlashcardRepository` and `FlashcardRepository` so study queries can filter by starred cards:

- Existing all-card query remains supported.
- Add an optional `starredOnly` path or a dedicated starred query.

### Service

Update `IFlashcardSetService` and `FlashcardSetService`:

- Expand `AddCardAsync`.
- Expand `UpdateCardAsync`.
- Validate and trim vocabulary fields.

Update `IStudyService` and `StudyService`:

- Expand `GetFlashcardsForStudyAsync` with `starredOnly`.
- Keep authorization checks from the existing private/public set protection.

### Controller

Update `FlashcardSetController`:

- Bind the new card fields from the edit page.
- Return validation errors to the same set edit page.
- Preserve current owner-only edit behavior.

Update `StudyController`:

- Accept `starredOnly` on `Flashcard`.
- Pass `starredOnly` into the study service.
- Show a clear message when no cards match.

### Views

Update:

- `Views/FlashcardSet/Edit.cshtml` for the approved vocabulary editor layout.
- `Views/Study/Index.cshtml` for "Học thẻ đã sao".
- `Views/Study/Flashcard.cshtml` for the all/starred toggle.
- Any detail/list card displays that should show star/IPA/part-of-speech summaries.

## Error Handling

- Missing required field: show validation error on the set edit page.
- Non-owner edit attempt: return forbid, matching existing behavior.
- Invalid card id: return not found.
- Starred study with no starred cards: redirect to `/Study/{setId}` with a message.
- Private set access by non-owner: return not found/forbid following existing study access behavior.

## Verification

Build:

```powershell
dotnet build /p:UseAppHost=false
```

Manual smoke tests:

1. Create a new vocabulary card with all required fields.
2. Try creating a card with missing IPA and confirm the edit page shows an error.
3. Mark a card as starred, save, reload, and confirm the star persists.
4. Study all cards and confirm front/back still use only term/definition.
5. Study starred cards and confirm only starred cards appear.
6. Try starred study on a set with no starred cards and confirm the app shows a useful message.
7. Confirm a non-owner cannot edit or study another user's private set.

## Out of Scope

- Separate `VocabularyCard` model.
- Separate metadata table.
- Audio pronunciation or text-to-speech.
- Bulk import/export.
- Search/filter by part of speech.
- Showing all vocabulary metadata during flashcard study.
