# Flashcard Vocabulary Panel Bugfix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the broken flashcard vocabulary panel implementation so the study page has valid Razor/HTML/JS, preserves old progress data, and removes unrelated committed files.

**Architecture:** Restore the study Razor view to the last known good version before the broken panel commit, then reapply only the vocabulary panel markup, data serialization, render helpers, and small state-sync hooks. Fix the existing EF migration in place with one SQL backfill because it has not been accepted yet. Keep the cleanup commit separate from functional fixes.

**Tech Stack:** ASP.NET Core MVC, Razor, EF Core migrations, SQL Server, vanilla JavaScript, existing `flashcard.css`.

---

## File Structure

- Modify `Views/Study/Flashcard.cshtml`: restore valid markup/JS, then add vocabulary panel in the correct locations.
- Modify `Migrations/20260628005116_AddUserProgressStatus.cs`: backfill `Status` from old `IsLearned` values.
- Restore `docs/superpowers/specs/2026-06-27-flashcard-settings-and-images-design.md`: undo accidental deletion from feature commit.
- Remove `.codegraph/.gitignore` from git tracking: keep CodeGraph local files out of feature history.
- Verify `wwwroot/css/flashcard.css`: keep the vocabulary panel styles if they match the page palette and do not duplicate unrelated blocks.

## Task 1: Restore The Study Razor View To A Valid Baseline

**Files:**
- Modify: `Views/Study/Flashcard.cshtml`

- [ ] **Step 1: Restore the file from the last good commit**

Run:

```powershell
git restore --source=649e3dd -- Views/Study/Flashcard.cshtml
```

Expected: `Views/Study/Flashcard.cshtml` returns to the state after data loading was added and before the broken panel markup commit.

- [ ] **Step 2: Verify duplicate/broken fragments are gone**

Run:

```powershell
Select-String -Path Views\Study\Flashcard.cshtml -Pattern 'utton>|<section id="completion-view"|<section class="control-dock"|input.addEventListener' -Context 1,1
```

Expected:
- No `utton>` match.
- Exactly one `<section id="completion-view"` match.
- Exactly one `<section class="control-dock"` match.
- `input.addEventListener` appears only inside `initializeSettingsPanel`.

- [ ] **Step 3: Commit the restore**

Run:

```powershell
git add Views/Study/Flashcard.cshtml
git commit -m "fix: restore valid flashcard study view"
```

Expected: one commit that only changes `Views/Study/Flashcard.cshtml`.

## Task 2: Reapply Vocabulary Panel Markup Safely

**Files:**
- Modify: `Views/Study/Flashcard.cshtml`

- [ ] **Step 1: Insert the panel after `.card-nav-strip`**

In `Views/Study/Flashcard.cshtml`, find the closing `</div>` for:

```razor
<div class="card-nav-strip">
```

Immediately after that closing `</div>`, before:

```razor
<section class="control-dock" aria-label="Điều khiển học">
```

insert:

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

- [ ] **Step 2: Confirm panel position**

Run:

```powershell
Select-String -Path Views\Study\Flashcard.cshtml -Pattern 'card-nav-strip|vocab-panel|control-dock'
```

Expected order:

```text
card-nav-strip
vocab-panel
control-dock
```

- [ ] **Step 3: Commit panel markup**

Run:

```powershell
git add Views/Study/Flashcard.cshtml
git commit -m "fix: place vocabulary panel below card navigation"
```

Expected: one commit containing only the clean panel markup insertion.

## Task 3: Reapply Vocabulary Panel JavaScript Without Breaking Existing Handlers

**Files:**
- Modify: `Views/Study/Flashcard.cshtml`

- [ ] **Step 1: Add serialized vocabulary data**

After the existing `let flashcards = ...;` block, add:

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
                CorrectCount = progress == null ? 0 : progress.CorrectCount,
                WrongCount = progress == null ? 0 : progress.WrongCount
            };
        })));
```

- [ ] **Step 2: Add safe helper functions**

Insert these helpers after `function speak(text, isVietnamese = false) { ... }` and before `function updateUI()`:

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

                const term = document.createElement('div');
                term.className = 'vocab-term';
                const termText = document.createElement('strong');
                termText.textContent = card.FrontText || '';
                const meta = document.createElement('span');
                meta.textContent = [card.PartOfSpeech, card.Pronunciation, card.Synonyms].filter(Boolean).join(' · ');
                term.append(termText, meta);

                const definition = document.createElement('div');
                definition.className = 'vocab-definition';
                const backText = document.createElement('strong');
                backText.textContent = card.BackText || '';
                const example = document.createElement('em');
                example.textContent = card.ExampleSentence || '';
                const status = document.createElement('small');
                status.textContent = statusLabel(card.Status);
                definition.append(backText, example, status);

                const actions = document.createElement('div');
                actions.className = 'vocab-actions';
                const star = document.createElement('i');
                star.className = card.IsStarred ? 'ph ph-star-fill' : 'ph ph-star';
                const audio = document.createElement('button');
                audio.type = 'button';
                audio.title = 'Phát giọng đọc';
                audio.innerHTML = '<i class="ph ph-speaker-high"></i>';
                audio.addEventListener('click', () => speak(card.FrontText || '', false));
                actions.append(star, audio);

                item.append(term, definition, actions);
                list.appendChild(item);
            });
        }
```

- [ ] **Step 3: Wire sort listener once**

Near the existing startup calls at the bottom of the script, before `updateUI();`, add:

```javascript
        document.getElementById('vocab-sort')?.addEventListener('change', renderVocabularyPanel);
        renderVocabularyPanel();
```

- [ ] **Step 4: Sync star changes into the panel**

In `toggleCurrentCardStar`, after each `updateStarUI(card.IsStarred);`, add:

```javascript
            const vocabCard = vocabularyCards.find(c => c.Id === card.Id);
            if (vocabCard) vocabCard.IsStarred = card.IsStarred;
            renderVocabularyPanel();
```

There should be four copies: optimistic update, success update, non-success revert, and catch revert.

- [ ] **Step 5: Sync progress changes into the panel**

In `markCard`, after updating `sessionStats` and before creating `verificationToken`, add:

```javascript
            const vocabCard = vocabularyCards.find(c => c.Id === card.Id);
            if (vocabCard) {
                vocabCard.Status = learned ? 2 : 1;
                if (learned) vocabCard.CorrectCount++;
                else vocabCard.WrongCount++;
                renderVocabularyPanel();
            }
```

- [ ] **Step 6: Check JavaScript block shape**

Run:

```powershell
Select-String -Path Views\Study\Flashcard.cshtml -Pattern 'function renderVocabularyPanel|document.addEventListener\('
```

Expected:
- `function renderVocabularyPanel` appears once.
- The keydown `document.addEventListener('keydown'...)` block still closes with `});` before `function initializeSettingsPanel()`.
- `input.addEventListener('change'...)` remains inside `initializeSettingsPanel`, not inside the keydown handler.

- [ ] **Step 7: Commit panel JavaScript**

Run:

```powershell
git add Views/Study/Flashcard.cshtml
git commit -m "fix: render vocabulary panel without breaking study script"
```

Expected: one commit containing JS-only changes to the Razor view.

## Task 4: Backfill Existing Learned Progress In Migration

**Files:**
- Modify: `Migrations/20260628005116_AddUserProgressStatus.cs`

- [ ] **Step 1: Add SQL backfill after the new columns**

In `Up`, immediately after the `WrongCount` `AddColumn` block, add:

```csharp
            migrationBuilder.Sql("UPDATE UserProgresses SET Status = 2, CorrectCount = 1 WHERE IsLearned = 1");
```

The full end of `Up` should be:

```csharp
            migrationBuilder.AddColumn<int>(
                name: "WrongCount",
                table: "UserProgresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE UserProgresses SET Status = 2, CorrectCount = 1 WHERE IsLearned = 1");
        }
```

- [ ] **Step 2: Verify migration text**

Run:

```powershell
Select-String -Path Migrations\20260628005116_AddUserProgressStatus.cs -Pattern 'UPDATE UserProgresses SET Status = 2'
```

Expected: one match inside `Up`.

- [ ] **Step 3: Commit migration fix**

Run:

```powershell
git add Migrations/20260628005116_AddUserProgressStatus.cs
git commit -m "fix: backfill learned progress status"
```

Expected: one commit containing only the migration SQL backfill.

## Task 5: Clean Unrelated Git Changes From Feature History

**Files:**
- Restore: `docs/superpowers/specs/2026-06-27-flashcard-settings-and-images-design.md`
- Untrack: `.codegraph/.gitignore`

- [ ] **Step 1: Restore the deleted spec file**

Run:

```powershell
git restore --source=688209a -- docs/superpowers/specs/2026-06-27-flashcard-settings-and-images-design.md
```

Expected: the old spec file exists again.

- [ ] **Step 2: Remove `.codegraph/.gitignore` from git tracking only**

Run:

```powershell
git rm --cached -- .codegraph/.gitignore
```

Expected: `.codegraph/.gitignore` is staged as deleted from git, but the local file may remain on disk.

- [ ] **Step 3: Commit cleanup**

Run:

```powershell
git add docs/superpowers/specs/2026-06-27-flashcard-settings-and-images-design.md
git commit -m "fix: remove unrelated vocabulary panel changes"
```

Expected: commit restores the spec and removes the tracked CodeGraph file.

## Task 6: Verify Build And Runtime Shape

**Files:**
- Verify: `Views/Study/Flashcard.cshtml`
- Verify: `wwwroot/css/flashcard.css`
- Verify: `Migrations/20260628005116_AddUserProgressStatus.cs`

- [ ] **Step 1: Run build inside the workspace**

Use a workspace output folder to avoid locked `bin` files:

```powershell
dotnet build --no-restore /p:UseAppHost=false /p:OutputPath=C:\it\ltwnc\.build-review\
```

Expected:

```text
Build succeeded.
0 Error(s)
```

- [ ] **Step 2: Check for known broken fragments**

Run:

```powershell
Select-String -Path Views\Study\Flashcard.cshtml -Pattern 'utton>|<section id="completion-view"|<section class="control-dock"|function renderVocabularyPanel|input.addEventListener'
```

Expected:
- `utton>` has no match.
- `completion-view` appears once.
- `control-dock` appears once.
- `function renderVocabularyPanel` appears once.
- `input.addEventListener` appears inside `initializeSettingsPanel`.

- [ ] **Step 3: Check git status**

Run:

```powershell
git -c core.excludesfile= status --short
```

Expected:

Only one untracked `.build-review/` entry may remain because the build created it. Delete it after verification:

```powershell
Remove-Item -LiteralPath .build-review -Recurse -Force
git -c core.excludesfile= status --short
```

Expected: clean output.

- [ ] **Step 4: Manual browser verification**

Run the app:

```powershell
dotnet run
```

Open:

```text
/Study/{setId}/Flashcard
```

Expected:
- Main flashcard flips.
- Progress mode buttons work.
- Settings panel opens and settings changes do not throw JS errors.
- Vocabulary panel appears below card navigation and above the voice control dock.
- Dropdown sorts without page reload.
- Vocabulary speaker plays the English term.
- No duplicate completion screen appears after the last card.
