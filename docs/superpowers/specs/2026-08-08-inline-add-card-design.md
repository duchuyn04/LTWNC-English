# Inline Add-Card Button Design

## Goal

Let users add a flashcard quickly from the bottom of any existing card instead of returning to the toolbar.

## User behavior

- Each card shows an icon-only `+` button at its bottom.
- The button has an accessible label and tooltip: `Thêm thẻ sau thẻ này`.
- Clicking it inserts one new card immediately after the clicked card.
- The new card is expanded and its `Thuật ngữ` input receives focus.
- The existing toolbar `Thêm thẻ` button remains unchanged and appends a card.

## Implementation

- Add the footer button to server-rendered cards in `Views/FlashcardSet/Editor.cshtml`.
- Add the same footer to cards created by `createEmptyCard()` in `wwwroot/js/unified-editor.js`.
- Centralize insertion in a small `addCardAfter(card)` helper:
  - create and bind a new temporary card;
  - insert it with `container.insertBefore(newCard, card.nextElementSibling)`;
  - renumber cards;
  - focus the new card's front input.
- Bind the footer button in `bindCardEvents()`.
- Keep existing autosave, validation, and finish behavior unchanged.
- Add only the CSS needed for a full-width, keyboard-accessible footer control in `wwwroot/css/unified-editor.css`.

## Error handling

The new card remains a client-side temporary card until it has valid term and definition text. Existing autosave and validation handle persistence and errors; no new API or database change is required.

## Verification

- Add a regression test covering the footer markup and insertion handler.
- Run the full test suite and Release build.
- Verify in production that clicking `+` inserts after the selected card and focuses the new term field.

## Scope

No changes to API contracts, database schema, top toolbar behavior, card ordering rules, or autosave semantics.
