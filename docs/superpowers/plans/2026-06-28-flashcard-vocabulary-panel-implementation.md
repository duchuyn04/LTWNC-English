# Flashcard Vocabulary Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only vocabulary panel below the flashcard study card with edit navigation, client-side sorting, and richer learning status.

**Architecture:** Keep the feature inside the existing Study flashcard page. The server loads the filtered study deck for the main flashcard and separately loads the full set for the vocabulary panel; JavaScript renders and sorts the panel client-side. Progress stays in `UserProgress` with a small enum and counters.

**Tech Stack:** ASP.NET Core MVC, Razor, EF Core, SQL Server, vanilla JavaScript, existing `flashcard.css`.

---

## File Structure

- Modify `Models/Entities/UserProgress.cs`: add status enum and counters.
- Modify `Services/StudyService.cs`: populate status/counters in `MarkLearnedAsync`, add a progress lookup method for one set.
- Modify `Models/ViewModels/Study/FlashcardStudyViewModel.cs`: add full-set vocabulary cards and progress lookup.
- Modify `Controllers/StudyController.cs`: load full vocabulary list and progress for the page.
- Modify `Views/Study/Flashcard.cshtml`: add vocabulary panel markup and JS rendering/sorting.
- Modify `wwwroot/css/flashcard.css`: style the panel using the existing study page background palette.
- Create EF migration: add `Status`, `CorrectCount`, `WrongCount` to `UserProgresses`.

## Task 1: Add Progress Status Fields

**Files:**
- Modify: `Models/Entities/UserProgress.cs`
- Modify: `Services/StudyService.cs`
- Create: EF-generated migration files in `Migrations/` from `dotnet ef migrations add AddUserProgressStatus`

- [ ] **Step 1: Update `UserProgress`**

Replace the class body with this shape, keeping existing namespace/usings:

```csharp
public enum UserProgressStatus
{
    Unlearned = 0,
    Learning = 1,
    Mastered = 2
}

public class UserProgress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardId { get; set; }

    public bool IsLearned { get; set; }

    public UserProgressStatus Status { get; set; } = UserProgressStatus.Unlearned;

    public int CorrectCount { get; set; }

    public int WrongCount { get; set; }

    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [ForeignKey(nameof(FlashcardId))]
    public Flashcard? Flashcard { get; set; }
}
```

- [ ] **Step 2: Update `MarkLearnedAsync`**

In `Services/StudyService.cs`, after finding or creating `progress`, set the fields in one place:

```csharp
progress.IsLearned = learned;
progress.Status = learned ? UserProgressStatus.Mastered : UserProgressStatus.Learning;
if (learned)
{
    progress.CorrectCount++;
}
else
{
    progress.WrongCount++;
}
progress.LastReviewed = DateTime.UtcNow;
```

When creating the row, only initialize keys first:

```csharp
progress = new UserProgress
{
    UserId = userId,
    FlashcardId = flashcardId
};
await _context.UserProgresses.AddAsync(progress);
```

- [ ] **Step 3: Add migration**

Run:

```powershell
dotnet ef migrations add AddUserProgressStatus
```

Expected: a new migration adds `Status`, `CorrectCount`, and `WrongCount` with default `0`.

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build --no-restore /p:UseAppHost=false /p:OutputPath=C:\tmp\ltwnc-build\
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add Models/Entities/UserProgress.cs Services/StudyService.cs Migrations
git commit -m "feat: track flashcard progress status"
```

## Task 2: Load Vocabulary Panel Data

**Files:**
- Modify: `Services/StudyService.cs`
- Modify: `Models/ViewModels/Study/FlashcardStudyViewModel.cs`
- Modify: `Controllers/StudyController.cs`

- [ ] **Step 1: Add service method**

Add this method to `StudyService`:

```csharp
public async Task<Dictionary<int, UserProgress>> GetProgressByCardIdAsync(int setId, string? userId)
{
    if (string.IsNullOrWhiteSpace(userId)) return new Dictionary<int, UserProgress>();

    return await _context.UserProgresses
        .Where(p => p.UserId == userId && p.Flashcard != null && p.Flashcard.FlashcardSetId == setId)
        .ToDictionaryAsync(p => p.FlashcardId);
}
```

- [ ] **Step 2: Extend view model**

Update `FlashcardStudyViewModel`:

```csharp
public List<Flashcard> VocabularyCards { get; set; } = new();
public Dictionary<int, UserProgress> ProgressByCardId { get; set; } = new();
```

- [ ] **Step 3: Load full-set cards in controller**

In `StudyController.Flashcard`, keep `cards` as the filtered study list, then add:

```csharp
var vocabularyCards = await _studyService.GetFlashcardsForStudyAsync(setId, false, false, user?.Id);
var progressByCardId = await _studyService.GetProgressByCardIdAsync(setId, user?.Id);
```

Set the model fields:

```csharp
VocabularyCards = vocabularyCards,
ProgressByCardId = progressByCardId,
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build --no-restore /p:UseAppHost=false /p:OutputPath=C:\tmp\ltwnc-build\
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```powershell
git add Services/StudyService.cs Models/ViewModels/Study/FlashcardStudyViewModel.cs Controllers/StudyController.cs
git commit -m "feat: load vocabulary panel data"
```

## Task 3: Render Vocabulary Panel

**Files:**
- Modify: `Views/Study/Flashcard.cshtml`
- Modify: `wwwroot/css/flashcard.css`

- [ ] **Step 1: Add panel markup**

Place this section after `.card-nav-strip` and before `.control-dock`:

```razor
<section class="vocab-panel" aria-label="Danh sách từ vựng">
    <div class="vocab-panel-toolbar">
        <a class="vocab-edit-link" href="/Set/@Model.SetId/Edit">
            <i class="ph ph-pencil-simple-line"></i>
            <span>Chỉnh sửa</span>
        </a>

        <select id="vocab-sort" class="vocab-sort-select" aria-label="Sắp xếp từ vựng">
            <option value="original">Thứ tự gốc</option>
            <option value="starred">Được đánh dấu sao trước</option>
            <option value="unlearned">Chưa học trước</option>
            <option value="learning">Đang học trước</option>
            <option value="mastered">Đã thành thạo trước</option>
        </select>
    </div>

    <div class="vocab-list-heading">
        <h2>Từ vựng trong bộ thẻ</h2>
        <span id="vocab-count"></span>
    </div>

    <div id="vocab-list" class="vocab-list"></div>
</section>
```

- [ ] **Step 2: Serialize panel data**

Below the existing `flashcards` constant, add:

```razor
const vocabularyCards = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.VocabularyCards.Select(c =>
{
    Model.ProgressByCardId.TryGetValue(c.Id, out var progress);
    return new
    {
        c.Id,
        c.FrontText,
        c.BackText,
        c.Pronunciation,
        c.PartOfSpeech,
        c.ExampleSentence,
        c.Synonyms,
        c.IsStarred,
        c.OrderIndex,
        Status = progress == null ? 0 : (int)progress.Status,
        CorrectCount = progress?.CorrectCount ?? 0,
        WrongCount = progress?.WrongCount ?? 0
    };
})));
```

- [ ] **Step 3: Add render functions**

Add these JS helpers before `updateUI()`:

```javascript
function statusLabel(status) {
    if (status === 2) return 'Đã thành thạo';
    if (status === 1) return 'Đang học';
    return 'Chưa học';
}

function sortedVocabularyCards() {
    const sort = document.getElementById('vocab-sort')?.value || 'original';
    const cards = [...vocabularyCards];

    const byOriginal = (a, b) => a.OrderIndex - b.OrderIndex;
    if (sort === 'starred') return cards.sort((a, b) => Number(b.IsStarred) - Number(a.IsStarred) || byOriginal(a, b));
    if (sort === 'unlearned') return cards.sort((a, b) => Number(a.Status !== 0) - Number(b.Status !== 0) || byOriginal(a, b));
    if (sort === 'learning') return cards.sort((a, b) => Number(a.Status !== 1) - Number(b.Status !== 1) || byOriginal(a, b));
    if (sort === 'mastered') return cards.sort((a, b) => Number(a.Status !== 2) - Number(b.Status !== 2) || byOriginal(a, b));
    return cards.sort(byOriginal);
}

function renderVocabularyPanel() {
    const list = document.getElementById('vocab-list');
    const count = document.getElementById('vocab-count');
    if (!list) return;

    const cards = sortedVocabularyCards();
    list.innerHTML = '';
    if (count) count.textContent = `${cards.length} từ`;

    cards.forEach(card => {
        const item = document.createElement('article');
        item.className = 'vocab-item';
        item.innerHTML = `
            <div class="vocab-term">
                <strong>${escapeHtml(card.FrontText || '')}</strong>
                <span>${escapeHtml([card.PartOfSpeech, card.Pronunciation, card.Synonyms].filter(Boolean).join(' · '))}</span>
            </div>
            <div class="vocab-definition">
                <strong>${escapeHtml(card.BackText || '')}</strong>
                <em>${escapeHtml(card.ExampleSentence || '')}</em>
                <small>${statusLabel(card.Status)}</small>
            </div>
            <div class="vocab-actions">
                <i class="${card.IsStarred ? 'ph ph-star-fill' : 'ph ph-star'}"></i>
                <button type="button" onclick="speak('${escapeJs(card.FrontText || '')}', false)" title="Phát giọng đọc">
                    <i class="ph ph-speaker-high"></i>
                </button>
            </div>`;
        list.appendChild(item);
    });
}

function escapeHtml(value) {
    const div = document.createElement('div');
    div.textContent = value;
    return div.innerHTML;
}

function escapeJs(value) {
    return value.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
}
```

- [ ] **Step 4: Wire sort and refresh**

After event listener setup, add:

```javascript
document.getElementById('vocab-sort')?.addEventListener('change', renderVocabularyPanel);
renderVocabularyPanel();
```

In `toggleCurrentCardStar`, after each `updateStarUI(...)`, also update the matching vocabulary card and rerender:

```javascript
const vocabCard = vocabularyCards.find(c => c.Id === card.Id);
if (vocabCard) vocabCard.IsStarred = card.IsStarred;
renderVocabularyPanel();
```

In `markCard`, before `nextCard();`, update local status:

```javascript
const vocabCard = vocabularyCards.find(c => c.Id === card.Id);
if (vocabCard) {
    vocabCard.Status = learned ? 2 : 1;
    if (learned) vocabCard.CorrectCount++;
    else vocabCard.WrongCount++;
    renderVocabularyPanel();
}
```

- [ ] **Step 5: Add CSS**

Append to `wwwroot/css/flashcard.css`:

```css
.vocab-panel {
    display: grid;
    gap: 1rem;
    margin-top: 0.5rem;
}

.vocab-panel-toolbar,
.vocab-list-heading,
.vocab-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
}

.vocab-edit-link,
.vocab-sort-select,
.vocab-item {
    border: 1px solid var(--line);
    background: rgba(255, 255, 255, 0.72);
    color: var(--ink);
    box-shadow: 0 8px 24px rgba(62, 53, 40, 0.05);
}

.vocab-edit-link {
    min-height: 42px;
    padding: 0 0.9rem;
    border-radius: 999px;
    display: inline-flex;
    align-items: center;
    gap: 0.45rem;
    text-decoration: none;
    font-weight: 800;
}

.vocab-sort-select {
    min-height: 42px;
    min-width: min(100%, 260px);
    border-radius: 14px;
    padding: 0 0.9rem;
    font-weight: 700;
}

.vocab-list-heading h2 {
    margin: 0;
    font-size: 1.15rem;
}

.vocab-list-heading span {
    color: var(--muted);
    font-weight: 800;
}

.vocab-list {
    display: grid;
    gap: 0.75rem;
}

.vocab-item {
    border-radius: 18px;
    padding: 1rem 1.1rem;
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto;
}

.vocab-term,
.vocab-definition {
    min-width: 0;
    display: grid;
    gap: 0.35rem;
}

.vocab-definition {
    border-left: 1px solid var(--line);
    padding-left: 1rem;
}

.vocab-term strong,
.vocab-definition strong {
    font-size: 1.05rem;
}

.vocab-term span,
.vocab-definition em,
.vocab-definition small {
    color: var(--muted);
    overflow-wrap: anywhere;
}

.vocab-actions {
    display: inline-flex;
    align-items: center;
    gap: 0.7rem;
}

.vocab-actions button {
    border: 0;
    background: transparent;
    color: inherit;
    padding: 0.25rem;
}

@media (max-width: 720px) {
    .vocab-panel-toolbar,
    .vocab-list-heading {
        align-items: stretch;
        flex-direction: column;
    }

    .vocab-sort-select {
        width: 100%;
    }

    .vocab-item {
        grid-template-columns: 1fr;
    }

    .vocab-definition {
        border-left: 0;
        border-top: 1px solid var(--line);
        padding-left: 0;
        padding-top: 0.75rem;
    }

    .vocab-actions {
        justify-content: flex-end;
    }
}
```

- [ ] **Step 6: Build**

Run:

```powershell
dotnet build --no-restore /p:UseAppHost=false /p:OutputPath=C:\tmp\ltwnc-build\
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```powershell
git add Views/Study/Flashcard.cshtml wwwroot/css/flashcard.css
git commit -m "feat: add vocabulary panel to study page"
```

## Task 4: Manual Verification

**Files:**
- Verify: `Views/Study/Flashcard.cshtml`
- Verify: `wwwroot/css/flashcard.css`

- [ ] **Step 1: Run app**

```powershell
dotnet run
```

Expected: app starts and prints a localhost URL.

- [ ] **Step 2: Open a flashcard study page**

Open:

```text
/Study/{setId}/Flashcard
```

Expected:
- Main flashcard still works.
- Vocabulary panel appears below the flashcard.
- Panel color matches the current study page background palette.

- [ ] **Step 3: Verify edit navigation**

Click `Chỉnh sửa`.

Expected: browser navigates to:

```text
/Set/{setId}/Edit
```

- [ ] **Step 4: Verify sorting**

Try each dropdown option.

Expected:
- `Thứ tự gốc`: `OrderIndex` ascending.
- `Được đánh dấu sao trước`: starred cards first.
- `Chưa học trước`: `Status = 0` first.
- `Đang học trước`: `Status = 1` first.
- `Đã thành thạo trước`: `Status = 2` first.

- [ ] **Step 5: Verify progress updates**

Use progress mode and mark one card `Chưa biết`, then one card `Đã biết`.

Expected:
- Unknown card becomes `Đang học`.
- Known card becomes `Đã thành thạo`.
- Dropdown order reflects the new status without page reload.

- [ ] **Step 6: Verify voice**

Click the vocabulary card speaker.

Expected: browser speaks the English term, not the Vietnamese definition.

- [ ] **Step 7: Final status**

Run:

```powershell
git status --short
```

Expected: only intentional files are changed; do not stage `.codegraph/` or unrelated deleted spec files unless the user explicitly asks.
