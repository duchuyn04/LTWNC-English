# Flashcard Study UI/UX Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the flashcard study screen to a cleaner, focused, minimal UI while preserving all existing functionality.

**Architecture:** Keep the ASP.NET Core MVC structure intact. Modify the Razor view `Views/Study/Flashcard.cshtml`, the stylesheet `wwwroot/css/flashcard.css`, and the inline JavaScript in the view. No backend changes.

**Tech Stack:** ASP.NET Core MVC, Razor, Bootstrap (existing), vanilla JavaScript, CSS custom properties.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Views/Study/Flashcard.cshtml` | Razor markup for the study screen: header, progress, card, control bar, accordions, settings drawer, completion screen. Contains inline JavaScript for behavior. |
| `wwwroot/css/flashcard.css` | All styles for the study screen: design tokens, layout, card, controls, accordions, drawer, responsive breakpoints. |

---

## Task 1: Restructure the Razor markup

**Files:**
- Modify: `Views/Study/Flashcard.cshtml:9-276`

- [ ] **Step 1: Backup the current view**

```bash
cp Views/Study/Flashcard.cshtml Views/Study/Flashcard.cshtml.bak
```

- [ ] **Step 2: Replace the header section**

Replace the existing `study-header` with a minimal header:

```html
<header class="study-header">
    <a class="exit-link" href="/Study/@Model.SetId" title="Backspace: thoát về trang học">
        <span class="material-symbols-outlined">arrow_back</span>
        <span class="exit-label">Thoát</span>
    </a>

    <div class="set-meta">
        <h1 class="set-title">@Model.SetTitle</h1>
    </div>

    <button type="button" class="icon-button study-settings-button" onclick="toggleSettingsDrawer(event)" title="Cài đặt học">
        <span class="material-symbols-outlined">tune</span>
    </button>
</header>
```

- [ ] **Step 3: Add a thin progress strip**

After the header, add:

```html
<div class="progress-strip">
    <div class="progress-track" aria-hidden="true">
        <div id="card-progress-fill" class="progress-fill"></div>
    </div>
    <span id="card-progress-text" class="progress-text">Thẻ 1 / 1</span>
</div>
```

- [ ] **Step 4: Wrap the card and simplify toplines**

Inside `card-front` and `card-back`:
- Keep `card-star-btn`.
- Replace the topline with a single small badge and subtle hint.
- Keep the content elements (`card-front-text`, `card-front-ipa`, etc.).
- Move audio controls to bottom-right but remove the duplicate autoplay checkbox from the card faces (autoplay lives in the settings drawer).

Example front face markup:

```html
<article class="card-face card-front">
    <button type="button" class="card-star-btn" onclick="toggleCurrentCardStar(event)" title="Đánh dấu sao">
        <span class="material-symbols-outlined">star</span>
    </button>
    <div class="card-topline">
        <span class="badge badge-blue">English</span>
    </div>
    <div class="card-content">
        <span id="card-front-text" class="card-word card-word-front">Loading...</span>
        <p id="card-front-ipa" class="card-detail-text" hidden></p>
        <p id="card-front-definition" class="card-detail-text" hidden></p>
        <img id="card-front-image" class="card-study-image" alt="Ảnh minh họa mặt trước" hidden />
    </div>
    <div class="card-audio-controls" onclick="event.stopPropagation()">
        <button type="button" class="card-audio-btn" onclick="playAudioCurrentCard()" title="Phát giọng đọc (Ctrl)">
            <span class="material-symbols-outlined">volume_up</span>
        </button>
    </div>
</article>
```

- [ ] **Step 5: Replace the control strip with the new control bar**

Replace `card-nav-strip` and `shortcut-strip` with:

```html
<div class="control-bar" aria-label="Điều khiển học">
    <button type="button" id="nav-left-btn" class="control-btn control-btn-prev" onclick="handleLeftAction()" title="Thẻ trước (←)">
        <span class="material-symbols-outlined">arrow_back</span>
    </button>

    <div class="control-center">
        <button type="button" id="flip-btn" class="control-btn control-btn-flip" onclick="toggleCardFlip()" title="Lật thẻ (Space)">
            <span class="material-symbols-outlined">flip</span>
            <span>Lật</span>
        </button>

        <div id="rating-group" class="rating-group" hidden>
            <button type="button" class="control-btn control-btn-rate control-btn-rate-unknown" onclick="markCard(false)" title="Chưa biết (1)">
                <span class="material-symbols-outlined">close</span>
                <span>Chưa biết</span>
            </button>
            <button type="button" class="control-btn control-btn-rate control-btn-rate-known" onclick="markCard(true)" title="Đã biết (2)">
                <span class="material-symbols-outlined">check</span>
                <span>Đã biết</span>
            </button>
        </div>
    </div>

    <button type="button" id="nav-right-btn" class="control-btn control-btn-next" onclick="handleRightAction()" title="Thẻ tiếp (→)">
        <span class="material-symbols-outlined">arrow_forward</span>
    </button>
</div>

<p class="keyboard-hint">
    <span><kbd>Space</kbd> lật</span>
    <span><kbd>←</kbd> <kbd>→</kbd> chuyển thẻ</span>
    <span><kbd>1</kbd> chưa biết · <kbd>2</kbd> đã biết</span>
    <span><kbd>Backspace</kbd> thoát</span>
</p>
```

- [ ] **Step 6: Convert learning modes into a collapsible accordion**

Replace the `learning-modes` section with:

```html
<details class="study-accordion">
    <summary class="study-accordion-summary">
        <span class="material-symbols-outlined">school</span>
        <span>Chế độ học</span>
        <span class="material-symbols-outlined accordion-chevron">expand_more</span>
    </summary>
    <div class="study-accordion-body">
        <div class="learning-modes-grid">
            <!-- existing 7 learning-chip buttons -->
        </div>
    </div>
</details>
```

- [ ] **Step 7: Convert vocabulary panel into a collapsible accordion**

Wrap the existing `vocab-panel` in a `<details class="study-accordion">` with summary "Từ trong bộ (@Model.Flashcards.Count từ)".

- [ ] **Step 8: Move inline settings popover into a slide-in drawer**

Replace the inline `study-settings-panel` with a drawer:

```html
<aside id="study-settings-drawer" class="study-settings-drawer" hidden>
    <div class="settings-drawer-header">
        <span class="settings-title">Cài đặt học</span>
        <button type="button" class="icon-button" onclick="toggleSettingsDrawer(event)" title="Đóng">
            <span class="material-symbols-outlined">close</span>
        </button>
    </div>
    <div class="settings-drawer-body">
        <!-- existing settings checkboxes, grouped -->
    </div>
</aside>
<div id="settings-drawer-backdrop" class="settings-drawer-backdrop" hidden onclick="toggleSettingsDrawer(event)"></div>
```

- [ ] **Step 9: Remove the old `control-dock` section**

Delete the entire `<section class="control-dock">` (TTS details moved into settings drawer).

- [ ] **Step 10: Verify the view builds**

Run:

```bash
dotnet build
```

Expected: build succeeds with no errors.

---

## Task 2: Update CSS design tokens and base layout

**Files:**
- Modify: `wwwroot/css/flashcard.css:1-44`

- [ ] **Step 1: Replace CSS custom properties**

```css
:root {
    --fc-ink: #1c1917;
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
    --fc-radius: 24px;
    --fc-shadow: 0 20px 50px rgba(0, 0, 0, 0.06);
    --fc-shadow-lg: 0 28px 70px rgba(0, 0, 0, 0.08);
}
```

- [ ] **Step 2: Reset base layout**

```css
html,
body {
    overflow-x: clip;
}

.study-shell {
    min-height: calc(100dvh - 58px);
    margin: -3rem calc(50% - 50vw);
    padding: 1.25rem clamp(1rem, 4vw, 2.5rem);
    color: var(--fc-ink);
    background: var(--fc-canvas);
}

.study-stage {
    width: min(100%, 760px);
    margin: 0 auto;
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
}
```

- [ ] **Step 3: Verify build still passes**

Run:

```bash
dotnet build
```

Expected: build succeeds.

---

## Task 3: Style the header and progress strip

**Files:**
- Modify: `wwwroot/css/flashcard.css` (replace existing header styles)

- [ ] **Step 1: Style the minimal header**

```css
.study-header {
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    gap: 1rem;
}

.exit-link,
.icon-button {
    border: 1px solid var(--fc-line);
    background: var(--fc-paper);
    color: var(--fc-ink);
    text-decoration: none;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.45rem;
    min-height: 42px;
    min-width: 42px;
    border-radius: 999px;
    font-weight: 700;
    transition: transform 180ms ease, border-color 180ms ease, background 180ms ease;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.04);
}

.exit-link {
    padding: 0 0.9rem;
}

.set-meta {
    text-align: center;
    min-width: 0;
}

.set-title {
    margin: 0;
    font-size: clamp(1.1rem, 2.2vw, 1.5rem);
    font-weight: 800;
    letter-spacing: -0.02em;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
```

- [ ] **Step 2: Style the progress strip**

```css
.progress-strip {
    display: flex;
    align-items: center;
    gap: 0.75rem;
}

.progress-track {
    flex: 1;
    height: 6px;
    border-radius: 999px;
    background: var(--fc-line);
    overflow: hidden;
}

.progress-fill {
    height: 100%;
    width: 0%;
    border-radius: inherit;
    background: var(--fc-accent);
    transition: width 300ms ease;
}

.progress-text {
    color: var(--fc-muted);
    font-size: 0.8rem;
    font-weight: 800;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
}
```

- [ ] **Step 3: Visual check**

Run the app, navigate to a flashcard set, confirm the header is compact and the progress strip appears.

---

## Task 4: Style the flashcard

**Files:**
- Modify: `wwwroot/css/flashcard.css` (replace existing card styles)

- [ ] **Step 1: Update card frame and core**

```css
.card-area {
    position: relative;
    perspective: 1200px;
    width: 100%;
}

.flashcard-frame {
    position: relative;
    width: min(100%, 720px);
    margin: 0 auto;
    padding: 0.75rem;
    border: 1px solid rgba(0, 0, 0, 0.08);
    border-radius: calc(var(--fc-radius) + 0.5rem);
    background: rgba(255, 255, 255, 0.65);
    box-shadow: var(--fc-shadow-lg);
    cursor: pointer;
    transition: transform 220ms ease, box-shadow 220ms ease;
}

.flashcard-frame:hover {
    transform: translateY(-2px);
    box-shadow: 0 32px 80px rgba(0, 0, 0, 0.09);
}

.flashcard-core {
    position: relative;
    height: clamp(360px, 52vh, 520px);
    border-radius: var(--fc-radius);
    transform-style: preserve-3d;
    transition: transform 520ms cubic-bezier(0.34, 1.56, 0.64, 1);
}

.flashcard-core.flipped {
    transform: rotateY(180deg);
}
```

- [ ] **Step 2: Update card faces**

```css
.card-face {
    position: absolute;
    inset: 0;
    display: grid;
    grid-template-rows: auto 1fr auto;
    align-items: center;
    padding: clamp(1.25rem, 3vw, 2rem);
    border: 1px solid var(--fc-line);
    border-radius: inherit;
    backface-visibility: hidden;
    overflow: hidden;
    background: var(--fc-paper);
}

.card-back {
    background: var(--fc-soft);
    transform: rotateY(180deg);
}

.card-topline {
    display: flex;
    justify-content: center;
}

.card-content {
    text-align: center;
    z-index: 1;
}

.card-word {
    display: block;
    max-width: 100%;
    overflow-wrap: anywhere;
    line-height: 1.05;
    letter-spacing: -0.04em;
}

.card-word-front {
    font-size: clamp(2.5rem, 7vw, 5rem);
    font-weight: 700;
    color: var(--fc-ink);
}

.card-word-back {
    font-size: clamp(2rem, 5vw, 3.5rem);
    font-weight: 700;
    color: var(--fc-success);
}

.card-detail-text {
    margin: 0.6rem 0 0;
    color: var(--fc-muted);
    font-weight: 500;
    font-size: 1rem;
    line-height: 1.5;
}

.card-audio-controls {
    position: absolute;
    right: 1.25rem;
    bottom: 1.25rem;
    z-index: 12;
}
```

- [ ] **Step 3: Update star button**

```css
.card-star-btn {
    position: absolute;
    top: 1rem;
    right: 1rem;
    z-index: 10;
    background: none;
    border: none;
    color: var(--fc-muted-light);
    font-size: 1.6rem;
    cursor: pointer;
    padding: 0.25rem;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    transition: transform 180ms ease, color 180ms ease;
}

.card-star-btn:hover {
    transform: scale(1.15);
    color: var(--fc-accent);
}

.card-star-btn.is-starred,
.card-star-btn.is-starred:hover {
    color: var(--fc-accent);
}
```

- [ ] **Step 4: Visual check**

Confirm the card looks centered, clean, and the flip still works.

---

## Task 5: Style the control bar and rating buttons

**Files:**
- Modify: `wwwroot/css/flashcard.css` (replace existing nav styles)

- [ ] **Step 1: Add control bar styles**

```css
.control-bar {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 1rem;
    margin: 0.5rem auto 0;
    width: min(100%, 720px);
}

.control-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.4rem;
    height: 48px;
    border: 1px solid rgba(0, 0, 0, 0.08);
    border-radius: 999px;
    background: var(--fc-paper);
    color: var(--fc-ink);
    font-weight: 800;
    cursor: pointer;
    transition: transform 160ms ease, background-color 160ms ease, box-shadow 160ms ease;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

.control-btn:hover {
    transform: translateY(-1px);
    box-shadow: 0 6px 16px rgba(0, 0, 0, 0.08);
}

.control-btn:active {
    transform: translateY(1px);
}

.control-btn-prev,
.control-btn-next {
    width: 48px;
    border-radius: 50%;
}

.control-center {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    min-width: 140px;
}

.control-btn-flip {
    width: 100%;
    background: var(--fc-ink);
    color: #fff;
    border-color: var(--fc-ink);
}

.rating-group {
    display: flex;
    gap: 0.75rem;
    width: 100%;
}

.control-btn-rate {
    flex: 1;
    padding: 0 1rem;
}

.control-btn-rate-unknown {
    background: var(--fc-error-bg);
    color: var(--fc-error);
    border-color: rgba(239, 68, 68, 0.2);
}

.control-btn-rate-known {
    background: var(--fc-success-bg);
    color: var(--fc-success);
    border-color: rgba(16, 185, 129, 0.2);
}
```

- [ ] **Step 2: Add keyboard hint**

```css
.keyboard-hint {
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
    gap: 0.5rem 1.25rem;
    margin: 0.75rem auto 0;
    color: var(--fc-muted-light);
    font-size: 0.75rem;
    font-weight: 700;
}

.keyboard-hint kbd {
    display: inline-block;
    padding: 0.1rem 0.4rem;
    border: 1px solid var(--fc-line);
    border-bottom-width: 2px;
    border-radius: 5px;
    background: var(--fc-soft);
    font-family: inherit;
}
```

- [ ] **Step 3: Visual check**

Confirm the control bar renders, the flip button is centered, and rating buttons appear after flipping.

---

## Task 6: Convert learning modes and vocab panel to accordions

**Files:**
- Modify: `wwwroot/css/flashcard.css`

- [ ] **Step 1: Add accordion styles**

```css
.study-accordion {
    width: min(100%, 720px);
    margin: 0 auto;
    border: 1px solid rgba(0, 0, 0, 0.08);
    border-radius: 1rem;
    background: var(--fc-paper);
    overflow: hidden;
}

.study-accordion-summary {
    list-style: none;
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.9rem 1.1rem;
    color: var(--fc-ink);
    font-weight: 800;
    cursor: pointer;
    user-select: none;
}

.study-accordion-summary::-webkit-details-marker {
    display: none;
}

.study-accordion-summary .accordion-chevron {
    margin-left: auto;
    transition: transform 200ms ease;
}

.study-accordion[open] .accordion-chevron {
    transform: rotate(180deg);
}

.study-accordion-body {
    padding: 0 1.1rem 1.1rem;
    animation: accordionOpen 250ms ease;
}

@keyframes accordionOpen {
    from {
        opacity: 0;
        transform: translateY(-6px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.learning-modes-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(90px, 1fr));
    gap: 0.6rem;
}

.learning-chip {
    background: var(--fc-soft);
    border: 1px solid var(--fc-line);
    color: var(--fc-muted);
    border-radius: 0.75rem;
    padding: 0.65rem 0.4rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.3rem;
    font-size: 0.72rem;
    font-weight: 700;
    transition: all 0.2s ease;
}

.learning-chip:hover:not(:disabled) {
    background: var(--fc-paper);
    border-color: var(--fc-line);
}

.learning-chip:disabled {
    opacity: 0.55;
    cursor: not-allowed;
}

.learning-chip.is-active {
    background: var(--fc-ink);
    border-color: var(--fc-ink);
    color: var(--fc-paper);
}

.learning-chip .material-symbols-outlined {
    font-size: 1.2rem;
}
```

- [ ] **Step 2: Adjust vocab panel inside accordion**

Scope the existing vocab styles so they still apply inside `.study-accordion-body`.

- [ ] **Step 3: Visual check**

Confirm both accordions are collapsed by default and open smoothly.

---

## Task 7: Implement the settings drawer

**Files:**
- Modify: `wwwroot/css/flashcard.css`

- [ ] **Step 1: Add drawer styles**

```css
.study-settings-drawer {
    position: fixed;
    top: 0;
    right: 0;
    width: min(100%, 360px);
    height: 100dvh;
    z-index: 100;
    display: flex;
    flex-direction: column;
    background: var(--fc-paper);
    box-shadow: -20px 0 60px rgba(0, 0, 0, 0.12);
    transform: translateX(0);
    transition: transform 250ms ease;
}

.study-settings-drawer[hidden] {
    display: block;
    transform: translateX(110%);
}

.settings-drawer-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 1rem 1.1rem;
    border-bottom: 1px solid var(--fc-line);
}

.settings-title {
    font-size: 1.1rem;
    font-weight: 800;
}

.settings-drawer-body {
    flex: 1;
    overflow-y: auto;
    padding: 1.1rem;
    display: grid;
    gap: 1.25rem;
}

.settings-drawer-body label {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    color: var(--fc-ink);
    font-size: 0.9rem;
    font-weight: 600;
    cursor: pointer;
}

.settings-group-title {
    font-size: 0.75rem;
    font-weight: 800;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--fc-muted);
    margin-bottom: 0.4rem;
}

.settings-drawer-backdrop {
    position: fixed;
    inset: 0;
    z-index: 99;
    background: rgba(0, 0, 0, 0.25);
}
```

- [ ] **Step 2: Visual check**

Confirm the settings button opens the drawer and the backdrop closes it.

---

## Task 8: Update JavaScript for new control states

**Files:**
- Modify: `Views/Study/Flashcard.cshtml` (inline JavaScript)

- [ ] **Step 1: Add drawer toggle function**

```javascript
function toggleSettingsDrawer(event) {
    if (event) event.stopPropagation();
    const drawer = document.getElementById('study-settings-drawer');
    const backdrop = document.getElementById('settings-drawer-backdrop');
    const isOpen = !drawer.hidden;
    drawer.hidden = isOpen;
    backdrop.hidden = isOpen;
}
```

- [ ] **Step 2: Update progress UI references**

Ensure `updateUI()` still targets `#card-progress-text` and `#card-progress-fill`.

- [ ] **Step 3: Add flip/rating visibility toggle**

Update `toggleCardFlip()` and `updateUI()` to show/hide `#flip-btn` and `#rating-group`:

```javascript
function updateControlBarForFlip() {
    const flipBtn = document.getElementById('flip-btn');
    const ratingGroup = document.getElementById('rating-group');
    if (!flipBtn || !ratingGroup) return;

    if (isFlipped) {
        flipBtn.hidden = true;
        ratingGroup.hidden = false;
    } else {
        flipBtn.hidden = false;
        ratingGroup.hidden = true;
    }
}
```

Call `updateControlBarForFlip()` at the end of `toggleCardFlip()` and inside `updateUI()` after resetting `isFlipped = false`.

- [ ] **Step 4: Keep keyboard shortcuts intact**

Ensure `1` and `2` still call `markCard(false)` and `markCard(true)`. Space flips. Arrows navigate. Backspace exits. Ctrl plays audio.

- [ ] **Step 5: Verify interaction**

Flip a card with Space or click, confirm rating buttons appear, press 1 or 2 to mark learned/unlearned.

---

## Task 9: Add card transition animation and responsive styles

**Files:**
- Modify: `wwwroot/css/flashcard.css`

- [ ] **Step 1: Add card change transition classes**

```css
.flashcard-core.is-exit-left {
    animation: cardExitLeft 220ms ease forwards;
}

.flashcard-core.is-exit-right {
    animation: cardExitRight 220ms ease forwards;
}

.flashcard-core.is-enter {
    animation: cardEnter 260ms ease forwards;
}

@keyframes cardExitLeft {
    to {
        opacity: 0;
        transform: translateX(-30px);
    }
}

@keyframes cardExitRight {
    to {
        opacity: 0;
        transform: translateX(30px);
    }
}

@keyframes cardEnter {
    from {
        opacity: 0;
        transform: translateX(var(--enter-direction, 30px));
    }
    to {
        opacity: 1;
        transform: translateX(0);
    }
}
```

- [ ] **Step 2: Wire animations in JavaScript**

In `nextCard()` and `prevCard()`, before calling `updateUI()`, add the exit animation, then swap the card, then add the enter animation. Example for `nextCard`:

```javascript
function nextCard() {
    if (currentIndex >= flashcards.length - 1) {
        showCompletionScreen();
        return;
    }

    const core = document.getElementById('study-card-core');
    core.classList.add('is-exit-right');

    setTimeout(() => {
        currentIndex++;
        updateUI();
        core.classList.remove('is-exit-right');
        core.style.setProperty('--enter-direction', '30px');
        core.classList.add('is-enter');
        setTimeout(() => core.classList.remove('is-enter'), 260);
    }, 220);
}
```

Do the mirror for `prevCard` with `is-exit-left` and `--enter-direction: -30px`.

- [ ] **Step 3: Add responsive styles**

```css
@media (max-width: 767px) {
    .study-header .exit-label {
        display: none;
    }

    .flashcard-frame {
        width: calc(100% - 2rem);
        border-radius: 20px;
    }

    .flashcard-core {
        height: auto;
        min-height: 280px;
    }

    .control-bar {
        flex-wrap: wrap;
        gap: 0.75rem;
    }

    .control-center {
        min-width: 120px;
        order: 1;
        flex: 1 1 100%;
    }

    .control-btn-prev,
    .control-btn-next {
        order: 0;
    }

    .control-btn-flip {
        order: 1;
    }

    .rating-group {
        order: 2;
        flex: 1 1 100%;
    }

    .keyboard-hint {
        display: none;
    }
}
```

- [ ] **Step 4: Visual check on mobile width**

Resize browser to <768px, confirm the card and controls are usable.

---

## Task 10: Final verification

**Files:**
- Modify: `Views/Study/Flashcard.cshtml` and `wwwroot/css/flashcard.css`

- [ ] **Step 1: Build the project**

```bash
dotnet build
```

Expected: build succeeds with no errors.

- [ ] **Step 2: Run and smoke-test**

```bash
dotnet run
```

Open `/Study/{setId}/Flashcard` and verify:

- The card is centered and visually dominant.
- Header is compact.
- Progress bar updates when navigating.
- Flip works by click and Space.
- Rating buttons appear after flipping.
- Star toggle works.
- Audio plays.
- Accordion sections collapse/expand.
- Settings drawer opens/closes.
- Keyboard shortcuts still work.
- Mobile layout is usable.

- [ ] **Step 3: Review for leftover dead CSS**

Search `wwwroot/css/flashcard.css` for selectors matching removed HTML classes (`card-nav-strip`, `shortcut-strip`, `control-dock`, `study-settings-panel`). Remove unused rules.

- [ ] **Step 4: Clean up backup file**

```bash
rm Views/Study/Flashcard.cshtml.bak
```

---

## Spec Coverage Check

| Spec Section | Implementing Task |
|--------------|-------------------|
| Minimal header + progress strip | Task 1, Task 3 |
| Large centered card | Task 1, Task 4 |
| Control bar with flip/rating | Task 1, Task 5, Task 8 |
| Collapsed learning modes / vocab | Task 1, Task 6 |
| Settings drawer | Task 1, Task 7, Task 8 |
| Color & typography | Task 2, Task 4 |
| Improved flip easing + card transition | Task 4, Task 9 |
| Responsive behavior | Task 9 |
| Preserved functionality | All tasks, especially Task 8 and Task 10 |
