# Empty Vocabulary Editor Design

## Goal

Render a complete, accessible empty state in the vocabulary list and detail columns when a flashcard set has no cards, without changing the existing editor when cards are present.

## Layout and behavior

- Keep the two-column `.vocab-editor` container visible so the page structure remains stable.
- In the list column, keep the heading `Danh sách từ` and replace the plain paragraph with a centered empty state containing an icon, `Chưa có từ vựng`, and a short explanation.
- In the detail column, render a centered empty state containing an icon, `Chưa có từ để chỉnh sửa`, explanatory text, and a link styled as a primary button.
- The detail action links to `#add-card-form`, using the existing add-card form rather than adding JavaScript or a second form.
- When at least one card exists, render the current card list, batch actions, and detail panels unchanged.
- On viewports at or below the existing mobile breakpoint, retain the existing single-column layout and avoid imposing the desktop synchronized editor height on the empty state.

## Accessibility and copy

- Decorative icons use `aria-hidden="true"`.
- The primary action remains a native anchor, so it works without JavaScript.
- List copy: `Chưa có từ vựng` and `Thêm từ đầu tiên để bắt đầu xây dựng bộ thẻ.`
- Detail copy: `Chưa có từ để chỉnh sửa` and `Thêm từ vựng đầu tiên, sau đó bạn có thể chỉnh sửa chi tiết tại đây.`
- Action copy: `Thêm từ vựng`.

## Implementation boundaries

- Modify `Views/FlashcardSet/Edit.cshtml` to conditionally render both empty-state blocks.
- Modify `wwwroot/css/edit.css` for shared empty-state styling and responsive sizing.
- Extend the existing markup and style tests; add no dependency, controller, service, database, or JavaScript changes.

## Verification

- A markup test proves both empty states, the action target, and accessible decorative icons are present.
- A style test proves the empty states are centered and the empty editor avoids the tall synchronized card-list height.
- Run the complete test project and visually inspect both an empty set and a populated set.
