# Inline Add Card Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an accessible `+` control below the last flashcard that inserts a new card after it and focuses the new term field.

**Architecture:** Keep the existing client-side editor and autosave flow. Add the same footer control to server-rendered cards and dynamically-created cards, then centralize insertion in `addCardAfter(card)` so numbering, event binding, and focus stay consistent.

**Tech Stack:** ASP.NET Core MVC/Razor, vanilla JavaScript, CSS, xUnit.

## Global Constraints

- No API or database changes.
- Keep the toolbar `Thêm thẻ` button and its append behavior.
- Reuse existing `createEmptyCard()`, `bindCardEvents()`, `updateCardNumbering()`, and autosave.
- Preserve keyboard accessibility with a real `<button>`, `aria-label`, tooltip, and at least 44px hit area.
- Do not touch unrelated working-tree files.

---

### Task 1: Add inline card insertion control

**Files:**
- Modify: `tests/ltwnc.Tests/Views/CardActionEditorViewTests.cs`
- Modify: `Views/FlashcardSet/Editor.cshtml`
- Modify: `wwwroot/js/unified-editor.js`
- Modify: `wwwroot/css/unified-editor.css`

**Interfaces:**
- `createEmptyCard(): HTMLElement` continues to create and bind a temporary card.
- Add `addCardAfter(card: HTMLElement): HTMLElement` in `unified-editor.js`; it returns the inserted card.
- The footer button uses `data-add-card-after` and class `btn-add-after`.

- [ ] **Step 1: Write the failing regression test**

Add one test to `CardActionEditorViewTests`:

```csharp
[Fact]
public void Editor_exposes_inline_add_card_control()
{
    string view = Read("Views/FlashcardSet/Editor.cshtml");
    string script = Read("wwwroot/js/unified-editor.js");
    string css = Read("wwwroot/css/unified-editor.css");

    Assert.Contains("data-add-card-after", view);
    Assert.Contains("data-add-card-after", script);
    Assert.Contains("function addCardAfter(card)", script);
    Assert.Contains("container.insertBefore(newCard, card.nextElementSibling)", script);
    Assert.Contains(".btn-add-after", css);
    Assert.Contains(".flashcard-card:last-child .card-add-after-wrap", css);
}
```

- [ ] **Step 2: Run the focused test and confirm it fails**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore --filter FullyQualifiedName~Editor_exposes_inline_add_card_control
```

Expected: FAIL because the footer control, insertion helper, and CSS selector do not exist yet.

- [ ] **Step 3: Add the footer to server-rendered cards**

In `Views/FlashcardSet/Editor.cshtml`, append this immediately after each card's existing `.card-body` and before `</article>`:

```html
<div class="card-add-after-wrap">
    <button type="button"
            class="btn-add-after"
            data-add-card-after
            aria-label="Thêm thẻ sau thẻ @card.FrontText"
            title="Thêm thẻ sau thẻ này">
        <i class="ph ph-plus" aria-hidden="true"></i>
    </button>
</div>
```

- [ ] **Step 4: Add the same footer to dynamically-created cards**

In `createEmptyCard()` in `wwwroot/js/unified-editor.js`, append this markup after the generated `.card-body`:

```html
<div class="card-add-after-wrap">
    <button type="button"
            class="btn-add-after"
            data-add-card-after
            aria-label="Thêm thẻ sau thẻ này"
            title="Thêm thẻ sau thẻ này">
        <i class="ph ph-plus" aria-hidden="true"></i>
    </button>
</div>
```

- [ ] **Step 5: Implement insertion and bind the button**

Add this helper after `createEmptyCard()`:

```javascript
function addCardAfter(card) {
    const newCard = createEmptyCard();
    container.insertBefore(newCard, card.nextElementSibling);
    updateCardNumbering();
    newCard.querySelector('.input-front').focus();
    return newCard;
}
```

At the start of `bindCardEvents(card)`, bind the footer button:

```javascript
card.querySelector('.btn-add-after')?.addEventListener('click', event => {
    event.stopPropagation();
    addCardAfter(card);
});
```

Leave the existing toolbar `addCard()` implementation unchanged so it continues appending to `container`.

- [ ] **Step 6: Add minimal accessible footer styling**

Add to `wwwroot/css/unified-editor.css` near the existing `.card-body` styles:

```css
.card-add-after-wrap {
    display: none;
    justify-content: center;
    padding: 0 var(--ue-space-md) var(--ue-space-sm);
    background: var(--ue-surface);
}

.flashcard-card:last-child .card-add-after-wrap {
    display: flex;
}

.btn-add-after {
    display: inline-grid;
    width: 44px;
    height: 44px;
    place-items: center;
    border: 1px solid var(--ue-line);
    border-radius: var(--radius-pill);
    background: var(--ue-surface);
    color: var(--ue-accent-deep);
    cursor: pointer;
}

.btn-add-after:hover,
.btn-add-after:focus-visible {
    border-color: var(--ue-accent-deep);
    background: var(--ue-sunken);
}
```

- [ ] **Step 7: Run the focused test and confirm it passes**

Run the focused command from Step 2. Expected: PASS.

- [ ] **Step 8: Run the complete verification**

Run:

```bash
dotnet test tests/ltwnc.Tests/ltwnc.Tests.csproj --no-restore
dotnet build ltwnc.csproj -c Release --no-restore
git diff --check
```

Expected: all tests pass, Release build has 0 warnings/errors, and `git diff --check` is clean.

- [ ] **Step 9: Commit the implementation**

```bash
git add Views/FlashcardSet/Editor.cshtml wwwroot/js/unified-editor.js wwwroot/css/unified-editor.css tests/ltwnc.Tests/Views/CardActionEditorViewTests.cs
git commit -m "feat(editor): add inline card button"
```

- [ ] **Step 10: Deploy and verify manually**

Push the commit to `master`, wait for the GitHub Actions deployment to succeed, then open a production editor page and verify:

1. Only the last card shows the bottom `+` control.
2. Clicking it inserts a new last card, expands it, and focuses its term input.
3. Typing a term and definition uses the existing autosave.
4. The toolbar `Thêm thẻ` still appends at the end.
