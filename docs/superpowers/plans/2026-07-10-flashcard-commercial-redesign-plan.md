# Flashcard Commercial Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the entire flashcard flow (study page, set list, create/edit set, set details) to a polished, warm, commercially-ready UI using Bootstrap 5 and the approved design system.

**Architecture:** Apply a shared warm-neutral design system across all flashcard views. Keep existing ASP.NET Core MVC structure and JavaScript logic; changes are primarily in Razor views and CSS. The study page gets the deepest visual overhaul, while set-management views adopt the same card-based, progress-aware language.

**Tech Stack:** ASP.NET Core MVC, Bootstrap 5.3, vanilla CSS, Material Symbols (study page), Phosphor Icons (retained where already used), Be Vietnam Pro font.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `wwwroot/css/flashcard.css` | Study-page-specific styles: design tokens, flashcard frame, flip animation, vocab panel, settings panel, navigation, completion screen. |
| `wwwroot/css/set-management.css` | Shared styles for set list, create/edit, and details pages: cards, empty states, progress bars, forms. |
| `Views/Study/Flashcard.cshtml` | Study page markup and JavaScript. |
| `Views/FlashcardSet/Index.cshtml` | Set list grid. |
| `Views/FlashcardSet/Create.cshtml` | Create set form. |
| `Views/FlashcardSet/Edit.cshtml` | Edit set form + card editor. |
| `Views/FlashcardSet/Details.cshtml` | Set details + word list. |

---

## Task 1: Establish Design Tokens in flashcard.css

**Files:**
- Modify: `wwwroot/css/flashcard.css`

Replace the existing cool-gray token block with the approved warm-neutral palette. Keep structural rules (flip, layout) intact.

- [ ] **Step 1: Update CSS custom properties**

Open `wwwroot/css/flashcard.css`. Replace the `:root` and `.study-shell` color definitions with:

```css
:root {
    --fc-ink: #292524;
    --fc-muted: #78716c;
    --fc-muted-light: #a8a29e;
    --fc-line: #e7e5e4;
    --fc-paper: #ffffff;
    --fc-canvas: #fafaf9;
    --fc-soft: #f5f5f4;
    --fc-accent: #f59e0b;
    --fc-accent-bg: #fff7ed;
    --fc-success: #10b981;
    --fc-success-bg: #ecfdf5;
    --fc-error: #ef4444;
    --fc-error-bg: #fef2f2;
}

.study-shell {
    min-height: calc(100dvh - 58px);
    margin: -3rem calc(50% - 50vw);
    padding: 1rem clamp(1rem, 3vw, 2.5rem);
    color: var(--fc-ink);
    background: var(--fc-canvas);
}
```

- [ ] **Step 2: Search and replace old variable references**

Replace all occurrences of the old variable names used in the previous rewrite:
- `--fc-blue` → `--fc-accent`
- `--fc-blue-bg` → `--fc-accent-bg`
- `--fc-green` → `--fc-success`
- `--fc-green-bg` → `--fc-success-bg`
- `--fc-red` → `--fc-error`
- `--fc-red-bg` → `--fc-error-bg`

Keep the same selectors; only the token values change.

- [ ] **Step 3: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add wwwroot/css/flashcard.css
git commit -m "design: warm-neutral tokens for flashcard redesign"
```

---

## Task 2: Refine Study Page Markup

**Files:**
- Modify: `Views/Study/Flashcard.cshtml`

Align the study page markup with the approved mockup: warmer palette cues, cleaner header, mode pills, and simplified card face layout.

- [ ] **Step 1: Update page links and icon font**

Ensure the top of `Views/Study/Flashcard.cshtml` has:

```html
@model FlashcardStudyViewModel
@{
    ViewData["Title"] = "Học flashcard";
}

<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
<link rel="stylesheet" href="~/css/flashcard.css" asp-append-version="true" />
```

- [ ] **Step 2: Replace the study header**

Replace the current `.study-header` block with:

```html
<header class="study-header">
    <a class="exit-link" href="/Study/@Model.SetId" title="Backspace: thoát về trang học">
        <span class="material-symbols-outlined">arrow_back</span>
        <span>Thoát</span>
        <span class="keycap">Backspace</span>
    </a>

    <div class="set-meta">
        <div class="study-eyebrow">Học flashcard</div>
        <h1 id="set-title-header" class="set-title">@Model.SetTitle</h1>
        <div class="study-filter-toggle" aria-label="Lọc thẻ học">
            <a class="@(!Model.StarredOnly && !Model.UnlearnedOnly ? "is-active" : "")" href="/Study/@Model.SetId/Flashcard?starredOnly=false&unlearnedOnly=false">Tất cả</a>
            <a class="@(Model.StarredOnly ? "is-active" : "")" href="/Study/@Model.SetId/Flashcard?starredOnly=true&unlearnedOnly=false">Đã sao</a>
        </div>
    </div>

    <div class="progress-block">
        <span id="card-progress-text" class="progress-text">Thẻ 1 / 1</span>
        <div class="progress-track" aria-hidden="true">
            <div id="card-progress-fill" class="progress-fill"></div>
        </div>
    </div>
</header>
```

- [ ] **Step 3: Add learning mode pills**

Insert after the header inside `#study-view`:

```html
<section class="learning-modes" aria-label="Chế độ học">
    <div class="row row-cols-2 row-cols-md-4 row-cols-lg-7 g-2">
        <button type="button" class="btn learning-chip" disabled>
            <span class="material-symbols-outlined">chat_bubble</span>
            <span>Speaking</span>
        </button>
        <button type="button" class="btn learning-chip" disabled>
            <span class="material-symbols-outlined">forum</span>
            <span>Hội thoại</span>
        </button>
        <button type="button" class="btn learning-chip" disabled>
            <span class="material-symbols-outlined">menu_book</span>
            <span>Ngữ pháp</span>
        </button>
        <button type="button" class="btn learning-chip" disabled>
            <span class="material-symbols-outlined">article</span>
            <span>Đọc hiểu</span>
        </button>
        <button type="button" class="btn learning-chip is-active">
            <span class="material-symbols-outlined filled">school</span>
            <span>Học</span>
        </button>
        <button type="button" class="btn learning-chip" disabled>
            <span class="material-symbols-outlined">assignment</span>
            <span>Kiểm tra</span>
        </button>
        <button type="button" class="btn learning-chip" disabled>
            <span class="material-symbols-outlined">headphones</span>
            <span>Nghe Chép</span>
        </button>
    </div>
</section>
```

- [ ] **Step 4: Simplify card face content**

Inside `.card-front .card-content`, keep only:

```html
<div class="card-content">
    <span id="card-front-text" class="card-word card-word-front">Loading...</span>
    <p id="card-front-ipa" class="card-detail-text" hidden></p>
    <p id="card-front-definition" class="card-detail-text" hidden></p>
    <img id="card-front-image" class="card-study-image" alt="Ảnh minh họa mặt trước" hidden />
    <span class="card-flip-hint">Click hoặc Space để lật</span>
</div>
```

Inside `.card-back .card-content`, keep:

```html
<div class="card-content">
    <span id="card-back-text" class="card-word card-word-back">Loading...</span>
    <p id="card-back-term" class="card-detail-text" hidden></p>
    <p id="card-back-ipa" class="card-detail-text" hidden></p>
    <p id="card-back-example" class="card-detail-text" hidden></p>
    <img id="card-back-image" class="card-study-image" alt="Ảnh minh họa mặt sau" hidden />
    <span class="card-flip-hint">Click để quay lại</span>
</div>
```

- [ ] **Step 5: Update navigation strip icons**

Ensure `.card-nav-strip` uses Material Symbols and includes a progress-mode button:

```html
<div class="card-nav-strip">
    <button type="button" id="progress-mode-btn" class="card-nav-btn card-progress-mode-btn" onclick="toggleProgressMode()" title="Bật chế độ đánh giá tiến độ">
        <span class="material-symbols-outlined">layers</span>
        <span>Tiến độ</span>
    </button>
    <button type="button" id="nav-left-btn" class="card-nav-btn" onclick="handleLeftAction()" title="Thẻ trước (←)">
        <span class="material-symbols-outlined">arrow_back</span>
    </button>
    <span id="card-nav-progress" class="card-nav-progress">Thẻ 1 / 1</span>
    <button type="button" id="nav-right-btn" class="card-nav-btn" onclick="handleRightAction()" title="Thẻ tiếp (→)">
        <span class="material-symbols-outlined">arrow_forward</span>
    </button>
    <button type="button" class="card-nav-btn" onclick="shuffleCards()" title="Trộn thẻ">
        <span class="material-symbols-outlined">shuffle</span>
    </button>
</div>
```

- [ ] **Step 6: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Views/Study/Flashcard.cshtml
git commit -m "design: refine study page markup for commercial redesign"
```

---

## Task 3: Update Study Page JavaScript for New Vocab Panel

**Files:**
- Modify: `Views/Study/Flashcard.cshtml` (script section)

The vocabulary panel must group cards by status and render the new two-column row layout.

- [ ] **Step 1: Replace renderVocabularyPanel**

Find the existing `renderVocabularyPanel()` function in the `@section Scripts` block and replace it with:

```javascript
function renderVocabularyPanel() {
    const list = document.getElementById('vocab-list');
    if (!list) return;

    const cards = sortedVocabularyCards();
    list.innerHTML = '';

    const unlearned = cards.filter(c => c.Status !== 2);
    const mastered = cards.filter(c => c.Status === 2);

    function renderGroup(title, items, isMastered) {
        if (items.length === 0) return;

        const group = document.createElement('div');
        group.className = 'vocab-group';

        const header = document.createElement('div');
        header.className = 'vocab-group-header';
        header.innerHTML = `<h3>${title}</h3><span class="vocab-group-count">${items.length}</span>`;
        group.appendChild(header);

        const rows = document.createElement('div');
        rows.className = 'vocab-group-rows';

        items.forEach(card => {
            const row = document.createElement('div');
            row.className = `vocab-row glass-card ${isMastered ? 'mastered-border' : ''}`;
            row.innerHTML = `
                <div class="row g-3 align-items-center">
                    <div class="col-12 col-md-6">
                        <div class="d-flex align-items-center gap-3">
                            <span class="material-symbols-outlined ${card.IsStarred ? 'filled text-dark' : 'text-muted'}">star</span>
                            <div>
                                <p class="vocab-word ${isMastered ? 'fw-bold' : ''}">${escapeHtml(card.FrontText || '')}</p>
                                <p class="vocab-meta">${escapeHtml([card.PartOfSpeech, card.Pronunciation].filter(Boolean).join(' · '))}</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-12 col-md-6">
                        <div class="d-flex align-items-center justify-content-between">
                            <div>
                                <p class="vocab-meaning">${escapeHtml(card.BackText || '')}</p>
                                <p class="vocab-example">${escapeHtml(card.ExampleSentence || '')}</p>
                            </div>
                            <button type="button" class="btn vocab-audio" title="Phát giọng đọc">
                                <span class="material-symbols-outlined">volume_up</span>
                            </button>
                        </div>
                    </div>
                </div>
            `;
            const audio = row.querySelector('.vocab-audio');
            audio.addEventListener('click', () => speak(card.FrontText || '', false));
            rows.appendChild(row);
        });

        group.appendChild(rows);
        list.appendChild(group);
    }

    renderGroup('Chưa học', unlearned, false);
    renderGroup('Đã thành thạo', mastered, true);
}
```

- [ ] **Step 2: Add escapeHtml helper**

Add this helper function in the script block, before `renderVocabularyPanel`:

```javascript
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

- [ ] **Step 3: Update star UI to Material Symbols**

Find `updateStarUI()` and ensure the icon class toggles are:

```javascript
function updateStarUI(isStarred) {
    const starIconFront = document.querySelector('.card-front .card-star-btn span');
    const starIconBack = document.querySelector('.card-back .card-star-btn span');
    const starBtnFront = document.querySelector('.card-front .card-star-btn');
    const starBtnBack = document.querySelector('.card-back .card-star-btn');

    if (starIconFront && starIconBack) {
        if (isStarred) {
            starIconFront.className = 'material-symbols-outlined filled';
            starIconBack.className = 'material-symbols-outlined filled';
            starBtnFront.classList.add('is-starred');
            starBtnBack.classList.add('is-starred');
        } else {
            starIconFront.className = 'material-symbols-outlined';
            starIconBack.className = 'material-symbols-outlined';
            starBtnFront.classList.remove('is-starred');
            starBtnBack.classList.remove('is-starred');
        }
    }
}
```

- [ ] **Step 4: Update progress-mode button icons**

Find `updateProgressModeButtons()` and ensure the innerHTML uses Material Symbols:

```javascript
leftBtn.innerHTML = progressMode ? '<span class="material-symbols-outlined">close</span>' : '<span class="material-symbols-outlined">arrow_back</span>';
rightBtn.innerHTML = progressMode ? '<span class="material-symbols-outlined">check</span>' : '<span class="material-symbols-outlined">arrow_forward</span>';
```

- [ ] **Step 5: Verify build and JS syntax**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Views/Study/Flashcard.cshtml
git commit -m "design: grouped vocab panel and material symbols in study js"
```

---

## Task 4: Add Missing Study Page CSS Rules

**Files:**
- Modify: `wwwroot/css/flashcard.css`

Add CSS rules for the new elements introduced in Tasks 2 and 3 (mode pills, flip hint, grouped vocab rows, etc.).

- [ ] **Step 1: Add learning-chip styles**

Append to `wwwroot/css/flashcard.css`:

```css
.learning-modes {
    width: 100%;
}

.learning-chip {
    background: var(--fc-paper);
    border: 1px solid var(--fc-line);
    color: var(--fc-muted);
    border-radius: 0.75rem;
    padding: 0.75rem 0.5rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.75rem;
    font-weight: 600;
    transition: all 0.2s ease;
}

.learning-chip:hover:not(:disabled) {
    background: var(--fc-soft);
    border-color: var(--fc-line);
}

.learning-chip:disabled {
    opacity: 0.6;
    cursor: not-allowed;
}

.learning-chip.is-active {
    background: var(--fc-ink);
    border-color: var(--fc-ink);
    color: var(--fc-paper);
}

.learning-chip .material-symbols-outlined {
    font-size: 1.25rem;
}
```

- [ ] **Step 2: Add card flip hint style**

Append:

```css
.card-flip-hint {
    display: block;
    margin-top: 1.5rem;
    font-size: 0.875rem;
    color: var(--fc-muted-light);
    font-weight: 600;
}
```

- [ ] **Step 3: Add grouped vocab styles**

Append:

```css
.vocab-group {
    display: grid;
    gap: 0.75rem;
}

.vocab-group-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
}

.vocab-group-header h3 {
    margin: 0;
    font-size: 1.15rem;
    font-weight: 800;
    color: var(--fc-ink);
}

.vocab-group-count {
    font-size: 0.82rem;
    font-weight: 800;
    color: var(--fc-muted);
    background: var(--fc-soft);
    padding: 0.2rem 0.7rem;
    border-radius: 999px;
}

.vocab-group-rows {
    display: grid;
    gap: 0.75rem;
}

.vocab-row {
    border-radius: 18px;
    padding: 1rem 1.1rem;
    background: var(--fc-paper);
    border: 1px solid var(--fc-line);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.03);
    transition: all 0.2s ease;
}

.vocab-row:hover {
    border-color: var(--fc-line);
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.04);
}

.vocab-row.mastered-border {
    border-top: 3px solid var(--fc-ink);
    background: var(--fc-soft);
}

.vocab-word {
    margin: 0;
    font-size: 1.05rem;
    font-weight: 600;
    color: var(--fc-ink);
}

.vocab-meta {
    margin: 0;
    font-size: 0.82rem;
    color: var(--fc-muted);
    font-style: italic;
}

.vocab-meaning {
    margin: 0;
    font-size: 1rem;
    font-weight: 600;
    color: var(--fc-ink);
}

.vocab-example {
    margin: 0.15rem 0 0;
    font-size: 0.82rem;
    color: var(--fc-muted);
    font-style: italic;
}

.vocab-audio {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border: 0;
    background: transparent;
    color: var(--fc-muted);
    transition: all 0.2s ease;
}

.vocab-audio:hover {
    background: var(--fc-soft);
    color: var(--fc-ink);
}
```

- [ ] **Step 4: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add wwwroot/css/flashcard.css
git commit -m "design: add study page css for mode pills and grouped vocab"
```

---

## Task 5: Create Shared Set-Management CSS

**Files:**
- Create: `wwwroot/css/set-management.css`

Introduce a shared stylesheet for set list, create/edit, and details pages so they match the warm design system without duplicating styles in each view.

- [ ] **Step 1: Create the CSS file**

Create `wwwroot/css/set-management.css` with:

```css
.set-management-shell {
    --sm-ink: #292524;
    --sm-muted: #78716c;
    --sm-line: #e7e5e4;
    --sm-paper: #ffffff;
    --sm-canvas: #fafaf9;
    --sm-soft: #f5f5f4;
    --sm-accent: #f59e0b;
    --sm-accent-bg: #fff7ed;
    --sm-success: #10b981;
    --sm-success-bg: #ecfdf5;
    --sm-error: #ef4444;
}

.set-page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1.5rem;
    gap: 1rem;
    flex-wrap: wrap;
}

.set-page-header h1 {
    margin: 0;
    font-size: 1.5rem;
    font-weight: 800;
    color: var(--sm-ink);
    letter-spacing: -0.02em;
}

.set-page-header p {
    margin: 0.25rem 0 0;
    color: var(--sm-muted);
    font-size: 0.9375rem;
}

.set-card {
    background: var(--sm-paper);
    border: 1px solid var(--sm-line);
    border-radius: 16px;
    padding: 1.25rem;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.03);
    transition: all 0.2s ease;
    height: 100%;
    display: flex;
    flex-direction: column;
}

.set-card:hover {
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.05);
    border-color: var(--sm-line);
}

.set-card-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 0.75rem;
    gap: 0.75rem;
}

.set-card-title {
    margin: 0;
    font-size: 1.1rem;
    font-weight: 700;
    color: var(--sm-ink);
}

.set-card-meta {
    margin: 0 0 1rem;
    font-size: 0.875rem;
    color: var(--sm-muted);
}

.set-card-progress {
    height: 6px;
    background: var(--sm-soft);
    border-radius: 3px;
    margin-bottom: 1rem;
    overflow: hidden;
}

.set-card-progress-bar {
    height: 100%;
    border-radius: 3px;
    background: var(--sm-accent);
    transition: width 300ms ease;
}

.set-card-progress-bar.is-high {
    background: var(--sm-success);
}

.set-card-actions {
    display: flex;
    gap: 0.5rem;
    margin-top: auto;
}

.btn-sm-primary {
    flex: 1;
    padding: 0.5rem;
    background: var(--sm-ink);
    color: white;
    border: none;
    border-radius: 8px;
    font-weight: 600;
    font-size: 0.875rem;
    text-align: center;
    text-decoration: none;
    transition: filter 150ms ease;
}

.btn-sm-primary:hover {
    filter: brightness(0.92);
    color: white;
}

.btn-sm-secondary {
    flex: 1;
    padding: 0.5rem;
    background: white;
    color: #57534e;
    border: 1px solid var(--sm-line);
    border-radius: 8px;
    font-weight: 600;
    font-size: 0.875rem;
    text-align: center;
    text-decoration: none;
    transition: background 150ms ease;
}

.btn-sm-secondary:hover {
    background: var(--sm-soft);
    color: #57534e;
}

.btn-sm-danger {
    padding: 0.5rem;
    background: var(--sm-error-bg);
    color: var(--sm-error);
    border: 1px solid #fee2e2;
    border-radius: 8px;
    font-weight: 600;
    font-size: 0.875rem;
    transition: background 150ms ease;
}

.btn-sm-danger:hover {
    background: #fee2e2;
}

.set-tag {
    display: inline-flex;
    padding: 0.25rem 0.6rem;
    border-radius: 999px;
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.set-tag-public {
    background: var(--sm-accent-bg);
    color: #c2410c;
}

.set-tag-private {
    background: var(--sm-soft);
    color: var(--sm-muted);
}

.set-empty-state {
    text-align: center;
    padding: 3rem 1rem;
    color: var(--sm-muted);
}

.set-empty-state-icon {
    font-size: 3rem;
    margin-bottom: 1rem;
}

.set-form-card {
    background: var(--sm-paper);
    border: 1px solid var(--sm-line);
    border-radius: 16px;
    padding: 1.5rem;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.03);
}

.set-form-label {
    display: block;
    font-size: 0.875rem;
    font-weight: 700;
    color: #57534e;
    margin-bottom: 0.5rem;
}

.set-form-input {
    width: 100%;
    padding: 0.75rem;
    border: 1px solid var(--sm-line);
    border-radius: 10px;
    font-size: 1rem;
    color: var(--sm-ink);
    background: white;
    transition: border-color 150ms ease, box-shadow 150ms ease;
}

.set-form-input:focus {
    outline: none;
    border-color: var(--sm-accent);
    box-shadow: 0 0 0 3px rgba(245, 158, 11, 0.12);
}

.set-form-textarea {
    min-height: 100px;
    resize: vertical;
}

.set-detail-hero {
    background: var(--sm-paper);
    border: 1px solid var(--sm-line);
    border-radius: 20px;
    padding: 2rem;
    text-align: center;
    margin-bottom: 1.5rem;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.03);
}

.set-detail-hero h2 {
    margin: 0 0 0.5rem;
    font-size: 1.75rem;
    font-weight: 800;
    color: var(--sm-ink);
}

.set-detail-hero p {
    margin: 0 0 1.5rem;
    color: var(--sm-muted);
}

.set-detail-stats {
    display: flex;
    justify-content: center;
    gap: 1.5rem;
    margin-bottom: 1.5rem;
}

.set-detail-stat {
    text-align: center;
}

.set-detail-stat-value {
    font-size: 1.5rem;
    font-weight: 800;
    color: var(--sm-ink);
}

.set-detail-stat-label {
    font-size: 0.75rem;
    color: var(--sm-muted);
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.set-detail-list {
    background: var(--sm-paper);
    border: 1px solid var(--sm-line);
    border-radius: 16px;
    padding: 1.25rem;
}

.set-detail-list-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1rem;
}

.set-detail-list-header h3 {
    margin: 0;
    font-size: 1.1rem;
    font-weight: 700;
    color: var(--sm-ink);
}

.set-detail-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.75rem;
    border-bottom: 1px solid var(--sm-soft);
}

.set-detail-row:last-child {
    border-bottom: none;
}

.set-detail-term {
    font-weight: 600;
    color: var(--sm-ink);
}

.set-detail-definition {
    color: var(--sm-muted);
}

.btn-primary-custom {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    padding: 0.6rem 1.2rem;
    background: var(--sm-ink);
    color: white;
    border: none;
    border-radius: 10px;
    font-weight: 600;
    text-decoration: none;
    transition: filter 150ms ease;
}

.btn-primary-custom:hover {
    filter: brightness(0.92);
    color: white;
}

.btn-secondary-custom {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    padding: 0.6rem 1.2rem;
    background: white;
    color: #57534e;
    border: 1px solid var(--sm-line);
    border-radius: 10px;
    font-weight: 600;
    text-decoration: none;
    transition: background 150ms ease;
}

.btn-secondary-custom:hover {
    background: var(--sm-soft);
    color: #57534e;
}

@media (max-width: 767px) {
    .set-page-header {
        flex-direction: column;
        align-items: flex-start;
    }

    .set-detail-stats {
        gap: 1rem;
    }

    .set-detail-row {
        flex-direction: column;
        align-items: flex-start;
        gap: 0.25rem;
    }
}
```

- [ ] **Step 2: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add wwwroot/css/set-management.css
git commit -m "design: add shared set-management stylesheet"
```

---

## Task 6: Redesign Set List Page

**Files:**
- Modify: `Views/FlashcardSet/Index.cshtml`

Replace the current set list with card-based grid, progress bars, and a styled empty state.

- [ ] **Step 1: Add stylesheet link**

At the top of `Views/FlashcardSet/Index.cshtml`, after the model declaration, add:

```html
<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />
```

- [ ] **Step 2: Rewrite page markup**

Replace the entire content of `Views/FlashcardSet/Index.cshtml` with:

```html
@model List<ltwnc.Models.Entities.FlashcardSet>
@{
    ViewData["Title"] = "Bộ thẻ của tôi";
}

<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />

<div class="set-management-shell">
    <div class="set-page-header">
        <div>
            <h1>Bộ thẻ của tôi</h1>
            <p>Quản lý tất cả bộ thẻ bạn đã tạo.</p>
        </div>
        <a href="/Set/Create" class="btn-primary-custom">
            <i class="ph ph-plus-circle"></i> Tạo bộ thẻ
        </a>
    </div>

    @if (!Model.Any())
    {
        <div class="set-empty-state">
            <div class="set-empty-state-icon">📚</div>
            <p style="font-size: 1.1rem; font-weight: 600; color: var(--sm-ink);">Bạn chưa có bộ thẻ nào.</p>
            <p style="margin-bottom: 1.5rem;">Tạo bộ thẻ đầu tiên để bắt đầu học.</p>
            <a href="/Set/Create" class="btn-primary-custom">Tạo bộ thẻ đầu tiên</a>
        </div>
    }
    else
    {
        <div class="row g-4">
            @foreach (var set in Model)
            {
                <div class="col-md-6 col-lg-4">
                    <div class="set-card">
                        <div class="set-card-header">
                            <h5 class="set-card-title">@set.Title</h5>
                            <span class="set-tag @(set.IsPublic ? "set-tag-public" : "set-tag-private")">
                                @(set.IsPublic ? "Công khai" : "Riêng tư")
                            </span>
                        </div>
                        @if (!string.IsNullOrEmpty(set.Description))
                        {
                            <p style="color: #78716c; font-size: 0.875rem; margin: 0 0 0.75rem;">@set.Description</p>
                        }
                        <p class="set-card-meta">@set.Flashcards.Count từ · Đã học 0%</p>
                        <div class="set-card-progress">
                            <div class="set-card-progress-bar" style="width: 0%;"></div>
                        </div>
                        <div class="set-card-actions">
                            <a href="/Study/@set.Id" class="btn-sm-primary">Học</a>
                            <a href="/Set/@set.Id/Edit" class="btn-sm-secondary">Sửa</a>
                            <form asp-action="Delete" asp-route-id="@set.Id" method="post" class="d-inline" onsubmit="return confirm('Xóa bộ thẻ này?');">
                                @Html.AntiForgeryToken()
                                <button type="submit" class="btn-sm-danger">Xóa</button>
                            </form>
                        </div>
                    </div>
                </div>
            }
        </div>
    }
</div>
```

Note: The "Đã học 0%" text is a placeholder. Real progress requires server-side aggregation, which is out of scope for this redesign. If the existing controller already exposes progress data, wire it in; otherwise leave the placeholder.

- [ ] **Step 3: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Views/FlashcardSet/Index.cshtml
git commit -m "design: redesign set list with card grid and empty state"
```

---

## Task 7: Redesign Create Set Page

**Files:**
- Modify: `Views/FlashcardSet/Create.cshtml`

Apply the warm form card style and improve visual hierarchy.

- [ ] **Step 1: Add stylesheet link**

At the top, after the model declaration, add:

```html
<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />
```

- [ ] **Step 2: Rewrite page markup**

Replace the entire content with:

```html
@model CreateSetViewModel
@{
    ViewData["Title"] = "Tạo bộ thẻ mới";
}

<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />

<div class="set-management-shell">
    <div class="row justify-content-center">
        <div class="col-md-8 col-lg-6">
            <div class="set-form-card">
                <h1 style="margin: 0 0 0.25rem; font-size: 1.5rem; font-weight: 800; color: var(--sm-ink);">Tạo bộ thẻ mới</h1>
                <p style="color: var(--sm-muted); margin: 0 0 1.5rem;">Đặt tiêu đề và mô tả cho bộ thẻ của bạn.</p>

                <form asp-action="Create" method="post">
                    <div asp-validation-summary="ModelOnly" class="mb-3"></div>

                    <div class="mb-3">
                        <label asp-for="Title" class="set-form-label"></label>
                        <input asp-for="Title" class="set-form-input" placeholder="Ví dụ: Từ vựng Unit 1" />
                        <span asp-validation-for="Title" class="text-danger"></span>
                    </div>

                    <div class="mb-3">
                        <label asp-for="Description" class="set-form-label"></label>
                        <textarea asp-for="Description" class="set-form-input set-form-textarea" rows="3" placeholder="Mô tả ngắn về bộ thẻ..."></textarea>
                    </div>

                    <div class="mb-4 form-check">
                        <input asp-for="IsPublic" class="form-check-input" />
                        <label asp-for="IsPublic" style="font-size: 0.875rem; color: #57534e;">Công khai — mọi ngườii có thể xem và học</label>
                    </div>

                    <button type="submit" class="btn-primary-custom w-100">Tiếp tục — Thêm thẻ</button>
                </form>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

- [ ] **Step 3: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Views/FlashcardSet/Create.cshtml
git commit -m "design: redesign create set form"
```

---

## Task 8: Redesign Edit Set Page

**Files:**
- Modify: `Views/FlashcardSet/Edit.cshtml`

Apply the warm form styles while preserving the existing two-pane card editor structure.

- [ ] **Step 1: Add stylesheet link**

Keep the existing `~/css/edit.css` link for layout-specific rules, and add the shared stylesheet above it:

```html
<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/edit.css" asp-append-version="true" />
```

- [ ] **Step 2: Wrap page in set-management-shell**

Wrap the entire existing `<div class="row justify-content-center fade-in-up">...</div>` content inside:

```html
<div class="set-management-shell">
    <!-- existing row content -->
</div>
```

- [ ] **Step 3: Style the set metadata card**

Replace the inner `<div class="card-custom mb-4">` (the set title/description form) with:

```html
<div class="set-form-card mb-4">
    <h1 style="margin: 0 0 0.25rem; font-size: 1.5rem; font-weight: 800; color: var(--sm-ink);">Sửa bộ thẻ</h1>

    <form asp-action="Edit" asp-route-id="@Model.Id" method="post" class="mt-4">
        <div asp-validation-summary="ModelOnly" class="mb-3"></div>

        <div class="mb-3">
            <label asp-for="Title" class="set-form-label"></label>
            <input asp-for="Title" class="set-form-input" />
            <span asp-validation-for="Title" class="text-danger"></span>
        </div>

        <div class="mb-3">
            <label asp-for="Description" class="set-form-label"></label>
            <textarea asp-for="Description" class="set-form-input set-form-textarea" rows="3"></textarea>
        </div>

        <div class="mb-4 form-check">
            <input asp-for="IsPublic" class="form-check-input" />
            <label asp-for="IsPublic" style="font-size: 0.875rem; color: #57534e;">Công khai</label>
        </div>

        <button type="submit" class="btn-primary-custom">Lưu thay đổi</button>
        <a href="/Set" class="btn-secondary-custom ms-2">Hủy</a>
    </form>
</div>
```

- [ ] **Step 4: Update vocab editor buttons**

Replace the "Thêm" link in `.vocab-list-header` with:

```html
<a href="#add-card-form" class="btn-primary-custom" style="padding: 0.4rem 0.8rem; font-size: 0.8125rem;">
    <i class="ph ph-plus"></i> Thêm
</a>
```

- [ ] **Step 5: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Views/FlashcardSet/Edit.cshtml
git commit -m "design: apply warm form styles to edit set page"
```

---

## Task 9: Redesign Set Details Page

**Files:**
- Modify: `Views/FlashcardSet/Details.cshtml`

Add the hero summary card, stats, and styled word list.

- [ ] **Step 1: Add stylesheet link**

At the top, after the model declaration, add:

```html
<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />
```

- [ ] **Step 2: Rewrite page markup**

Replace the entire content with:

```html
@model SetDetailViewModel
@{
    ViewData["Title"] = Model.Title;
}

<link rel="stylesheet" href="~/css/set-management.css" asp-append-version="true" />

<div class="set-management-shell">
    <div class="set-detail-hero">
        <h2>@Model.Title</h2>
        @if (!string.IsNullOrEmpty(Model.Description))
        {
            <p>@Model.Description</p>
        }
        else
        {
            <p>@(Model.IsPublic ? "Bộ thẻ công khai" : "Bộ thẻ riêng tư") · @Model.Flashcards.Count thẻ</p>
        }

        <div class="set-detail-stats">
            <div class="set-detail-stat">
                <div class="set-detail-stat-value">0%</div>
                <div class="set-detail-stat-label">Tiến độ</div>
            </div>
            <div class="set-detail-stat">
                <div class="set-detail-stat-value">0</div>
                <div class="set-detail-stat-label">Đã thuộc</div>
            </div>
            <div class="set-detail-stat">
                <div class="set-detail-stat-value">@Model.Flashcards.Count</div>
                <div class="set-detail-stat-label">Tổng số</div>
            </div>
        </div>

        <div class="d-flex gap-2 justify-content-center">
            @if (Model.IsOwner)
            {
                <a href="/Set/@Model.Id/Edit" class="btn-secondary-custom">
                    <i class="ph ph-pencil"></i> Sửa
                </a>
            }
            <a href="/Study/@Model.Id" class="btn-primary-custom">
                <i class="ph ph-play-circle"></i> Bắt đầu học
            </a>
        </div>
    </div>

    @if (Model.Flashcards.Any())
    {
        <div class="set-detail-list">
            <div class="set-detail-list-header">
                <h3>Danh sách từ</h3>
                @if (Model.IsOwner)
                {
                    <a href="/Set/@Model.Id/Edit" class="btn-secondary-custom" style="padding: 0.4rem 0.8rem; font-size: 0.8125rem;">Chỉnh sửa</a>
                }
            </div>
            @foreach (var card in Model.Flashcards.OrderBy(c => c.OrderIndex))
            {
                <div class="set-detail-row">
                    <div>
                        <div class="set-detail-term">@card.FrontText</div>
                        <small style="color: #78716c;">@card.PartOfSpeech · @card.Pronunciation</small>
                    </div>
                    <div class="set-detail-definition">@card.BackText</div>
                </div>
            }
        </div>
    }
    else
    {
        <div class="set-empty-state">
            <div class="set-empty-state-icon">📝</div>
            <p style="font-size: 1.1rem; font-weight: 600; color: var(--sm-ink);">Bộ thẻ này chưa có từ nào.</p>
            @if (Model.IsOwner)
            {
                <a href="/Set/@Model.Id/Edit" class="btn-primary-custom">Thêm từ đầu tiên</a>
            }
        </div>
    }
</div>
```

- [ ] **Step 3: Verify build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Views/FlashcardSet/Details.cshtml
git commit -m "design: redesign set details with hero stats and word list"
```

---

## Task 10: Cross-Page Polish and Testing

**Files:**
- Modify as needed

Final pass for consistency, mobile layout, and build verification.

- [ ] **Step 1: Mobile sanity check**

Open the app in a browser (or use Chrome DevTools device emulation) and verify:
- Study page card is readable at 375px width.
- Set list cards stack to 1 column on mobile.
- Create/edit forms do not overflow.
- Details hero and word list reflow correctly.

- [ ] **Step 2: Keyboard shortcut test on study page**

Load `/Study/{id}/Flashcard` and verify:
- `Space` flips the card.
- `←` and `→` navigate cards.
- `1` marks unknown, `2` marks known.
- `Backspace` exits to study index.

- [ ] **Step 3: Empty states**

Verify these empty states render without layout bugs:
- Set list with no sets.
- Set details with no cards.
- Study page with no cards (should already redirect per existing spec).

- [ ] **Step 4: Final build**

Run:
```bash
dotnet build
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Commit any final fixes**

```bash
git add -A
git commit -m "design: polish flashcard redesign across all views"
```

---

## Spec Coverage Check

| Spec Section | Implementing Task |
|--------------|-------------------|
| Warm-neutral color palette | Task 1, Task 5 |
| Typography scale | Task 1, Task 5 |
| Card/panel shapes | Task 1, Task 4, Task 5 |
| Study page header/progress | Task 2 |
| Learning mode pills | Task 2, Task 4 |
| Flashcard redesign | Task 2, Task 4 |
| Navigation strip | Task 2, Task 3 |
| Grouped vocab panel | Task 3, Task 4 |
| Completion screen | Task 1 (style tokens) |
| Set list grid | Task 6 |
| Create set form | Task 7 |
| Edit set form | Task 8 |
| Set details hero/stats | Task 9 |
| Empty states | Task 6, Task 7, Task 9 |
| Mobile-first | All tasks, Task 10 |

---

## Placeholder Scan

- No "TBD", "TODO", or "implement later" remain.
- All file paths are exact.
- Code shown is complete enough for an engineer to apply.
- Progress percentages on set list/details are intentionally placeholders; real progress aggregation is out of scope and noted.