# Flashcard Focus Deck Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Make the flashcard study screen compact and low-distraction without changing its learning behavior.

**Architecture:** Keep the existing Razor view and vanilla JavaScript. Consolidate controls into one action rail, use native details for vocabulary support, and update only the existing study stylesheet.

**Tech Stack:** ASP.NET Core MVC, Razor, Bootstrap 5.3, vanilla JavaScript, CSS, Material Symbols.

## Global Constraints

- Modify only Views/Study/Flashcard.cshtml and wwwroot/css/flashcard.css.
- Preserve flip, navigation, star, TTS, settings, shuffle, progress marking, and Space, Arrow, 1, 2, and Backspace shortcuts.
- Add no dependency, backend work, route, model, or endpoint.
- Primary icon controls remain at least 40px; no horizontal overflow at 375px or desktop width.

---

## File Structure

| File | Responsibility |
|---|---|
| Views/Study/Flashcard.cshtml | Focused control order, vocabulary disclosure, and its summary count. |
| wwwroot/css/flashcard.css | Compact card, action rail, disclosure, responsive, and reduced-motion rules. |

---

### Task 1: Consolidate the study markup

**Files:**

- Modify: Views/Study/Flashcard.cshtml:107-241
- Modify: Views/Study/Flashcard.cshtml:447-511

**Interfaces:**

- Consume existing toggleProgressMode(), handleLeftAction(), handleRightAction(), shuffleCards(), renderVocabularyPanel(), tts-voice-select, and tts-speed.
- Produce #vocab-summary-count for renderVocabularyPanel().

- [ ] **Step 1: Remove duplicate card hints**

Replace both card-topline blocks with these and remove both card-bottomline blocks. Keep the existing central card-flip-hint, star buttons, settings, and audio controls.

~~~
<div class="card-topline">
    <span class="badge badge-blue">English</span>
</div>
<!-- Keep the existing card-content and card-audio-controls after this block. -->
<div class="card-topline">
    <span class="badge badge-green">Nghĩa Việt</span>
</div>
~~~

- [ ] **Step 2: Replace the navigation, shortcut, vocabulary, and control-dock blocks**

Replace the four blocks after the card main element with this structure. Keep every existing button ID, event handler, select ID, and option value unchanged.

~~~
<div class="study-action-rail" aria-label="Điều khiển học">
    <button type="button" id="progress-mode-btn" class="card-nav-btn card-progress-mode-btn" onclick="toggleProgressMode()" title="Bật chế độ đánh giá tiến độ"><span class="material-symbols-outlined">layers</span><span>Tiến độ</span></button>
    <button type="button" id="nav-left-btn" class="card-nav-btn" onclick="handleLeftAction()" title="Thẻ trước (←)"><span class="material-symbols-outlined">arrow_back</span></button>
    <span id="card-nav-progress" class="card-nav-progress">Thẻ 1 / 1</span>
    <button type="button" id="nav-right-btn" class="card-nav-btn" onclick="handleRightAction()" title="Thẻ tiếp (→)"><span class="material-symbols-outlined">arrow_forward</span></button>
    <button type="button" class="card-nav-btn" onclick="shuffleCards()" title="Trộn thẻ"><span class="material-symbols-outlined">shuffle</span></button>
    <details class="tts-details">
        <summary class="tts-summary" title="Giọng đọc"><span class="material-symbols-outlined">tune</span><span>Giọng đọc</span></summary>
        <div class="tts-panel">
            <div class="tts-row"><label for="tts-voice-select">Giọng EN</label><select id="tts-voice-select" class="form-select premium-select"></select></div>
            <div class="tts-row"><label for="tts-speed">Tốc độ</label><select id="tts-speed" class="form-select premium-select"><option value="0.8">0.8x</option><option value="1.0" selected>1.0x</option><option value="1.2">1.2x</option></select></div>
        </div>
    </details>
</div>

<details class="vocab-panel">
    <summary class="vocab-panel-summary"><span><span class="material-symbols-outlined">menu_book</span>Từ trong bộ</span><span id="vocab-summary-count" class="vocab-summary-count">0 từ</span></summary>
    <div class="vocab-panel-content">
        <div class="vocab-panel-toolbar">
            <a class="vocab-edit-link" href="/Set/@Model.SetId/Edit"><span class="material-symbols-outlined">edit</span><span>Chỉnh sửa</span></a>
            <select id="vocab-sort" class="form-select vocab-sort-select" aria-label="Sắp xếp từ vựng"><option value="original">Thứ tự gốc</option><option value="starred">Được đánh dấu sao trước</option><option value="unlearned">Chưa học trước</option><option value="learning">Đang học trước</option><option value="mastered">Đã thành thạo trước</option></select>
        </div>
        <div id="vocab-list" class="vocab-list"></div>
    </div>
</details>
~~~

- [ ] **Step 3: Synchronize the vocabulary disclosure count**

In renderVocabularyPanel(), immediately after const cards = sortedVocabularyCards(); add:

~~~
const summaryCount = document.getElementById('vocab-summary-count');
if (summaryCount) summaryCount.textContent = `${cards.length} từ`;
~~~

- [ ] **Step 4: Verify and commit**

Run: dotnet build

Expected: Build succeeded., 0 Warning(s), 0 Error(s).

~~~
git add Views/Study/Flashcard.cshtml
git commit -m "design: consolidate flashcard study controls"
~~~

---

### Task 2: Apply compact Focus Deck styling

**Files:**

- Modify: wwwroot/css/flashcard.css:39-930

**Interfaces:**

- Consume .study-action-rail, .vocab-panel-summary, .vocab-summary-count, and .vocab-panel-content from Task 1.
- Preserve existing JavaScript selectors and the #study-card-core flip animation.

- [ ] **Step 1: Replace the stage, frame, and core sizing rules**

~~~
.study-stage { width: min(100%, 1080px); margin: 0 auto; display: grid; gap: 1rem; }
.flashcard-frame { position: relative; width: min(100%, 680px); margin: 0 auto; padding: 0.55rem; border: 1px solid var(--fc-line); border-radius: 1.25rem; background: var(--fc-paper); box-shadow: 0 16px 40px rgba(41, 37, 36, 0.08); cursor: pointer; transition: transform 220ms ease, box-shadow 220ms ease; }
.flashcard-core { position: relative; height: clamp(340px, 48vh, 420px); border-radius: 0.95rem; transform-style: preserve-3d; transition: transform 520ms cubic-bezier(0.2, 0.75, 0.2, 1); }
~~~

- [ ] **Step 2: Delete obsolete card-nav-strip, shortcut-strip, control-dock, nav-cluster, and audio-cluster rules and add the action rail**

~~~
.study-action-rail { width: min(100%, 680px); margin: 0 auto; display: flex; align-items: center; justify-content: center; flex-wrap: wrap; gap: 0.5rem; }
.card-nav-btn, .tts-summary { min-height: 42px; }
.card-nav-btn { width: 42px; height: 42px; border-radius: 0.75rem; }
.card-progress-mode-btn { width: auto; padding: 0 0.75rem; }
.card-nav-progress { min-width: 4.5rem; font-variant-numeric: tabular-nums; }
.tts-summary { padding: 0 0.75rem; border-radius: 0.75rem; }
~~~

- [ ] **Step 3: Add compact mode and vocabulary disclosure rules**

~~~
.learning-modes .row { --bs-gutter-x: 0.35rem; --bs-gutter-y: 0.35rem; }
.learning-chip { min-height: 56px; padding: 0.45rem 0.35rem; border-radius: 0.7rem; }
.vocab-panel { width: min(100%, 900px); margin: 0.5rem auto 0; border: 1px solid var(--fc-line); border-radius: 1rem; background: var(--fc-paper); }
.vocab-panel-summary { min-height: 48px; padding: 0.75rem 1rem; display: flex; align-items: center; justify-content: space-between; gap: 1rem; cursor: pointer; color: var(--fc-ink); font-weight: 800; list-style: none; }
.vocab-panel-summary::-webkit-details-marker { display: none; }
.vocab-panel-summary > span:first-child { display: inline-flex; align-items: center; gap: 0.45rem; }
.vocab-summary-count { color: var(--fc-muted); font-size: 0.82rem; font-variant-numeric: tabular-nums; }
.vocab-panel-content { display: grid; gap: 1rem; padding: 0 1rem 1rem; }
~~~

- [ ] **Step 4: Add mobile and reduced-motion rules**

~~~
@media (max-width: 767px) {
    .flashcard-core { height: clamp(320px, 50vh, 390px); }
    .study-action-rail { gap: 0.4rem; }
    .tts-summary span:last-child, .card-progress-mode-btn span:last-child { display: none; }
    .vocab-panel-content { padding: 0 0.75rem 0.75rem; }
}
@media (prefers-reduced-motion: reduce) {
    .flashcard-core, .flashcard-frame, .card-nav-btn, .learning-chip, .vocab-row { transition: none; }
}
~~~

- [ ] **Step 5: Verify and commit**

Run: dotnet build

Expected: Build succeeded., 0 Warning(s), 0 Error(s).

~~~
git add wwwroot/css/flashcard.css
git commit -m "design: compact flashcard focus deck"
~~~

---

### Task 3: Verify the study flow in a browser

**Files:**

- No source changes expected.

- [ ] **Step 1: Build and start the application**

Run: dotnet build

Expected: Build succeeded., 0 Warning(s), 0 Error(s).

Run: dotnet run --no-build --launch-profile http

Expected: Now listening on: http://localhost:5000.

- [ ] **Step 2: Verify desktop and mobile behavior**

Open /Study/{id}/Flashcard with at least two cards. At 1280x900, verify all seven modes share one row, the card does not exceed 420px, the action rail contains navigation and voice settings, and vocabulary is closed initially. At 375x812, verify document.documentElement.scrollWidth === document.documentElement.clientWidth and test Space, ArrowLeft, ArrowRight, 1, 2, Backspace, voice settings, sorting, and vocabulary disclosure.

- [ ] **Step 3: Commit only a browser-found source fix**

~~~
git add Views/Study/Flashcard.cshtml wwwroot/css/flashcard.css
git commit -m "design: polish flashcard focus deck"
~~~

Do not create this commit if browser verification finds no source change.

---

## Spec Coverage Check

| Requirement | Task |
|---|---|
| Compact header, mode strip, and 420px card | Task 2 |
| One flip hint and one action rail | Tasks 1-2 |
| Collapsed vocabulary support area | Tasks 1-2 |
| Existing controls and keyboard flow | Tasks 1 and 3 |
| Touch targets, reduced motion, responsive layout | Task 2 |
| Browser verification | Task 3 |
