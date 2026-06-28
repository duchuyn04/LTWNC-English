# Flashcard Vocabulary Panel Design

## Goal

Add a vocabulary panel under the flashcard study area so learners can review all words in the current set without leaving the study page.

## UI

Place the panel below the main flashcard.

- Top row:
  - Left: `Chỉnh sửa` button with edit icon.
  - Right: sort dropdown.
- Sort options:
  - `Thứ tự gốc`
  - `Được đánh dấu sao trước`
  - `Chưa học trước`
  - `Đang học trước`
  - `Đã thành thạo trước`
- List area:
  - Shows all vocabulary cards in the set.
  - Cards are read-only and do not jump the main flashcard.
  - Each card shows term, part of speech, IPA, synonyms, definition, example, star icon, and voice icon.

The panel must use the same dark background palette as the current study page. It should not introduce a separate lighter card theme.

## Behavior

- `Chỉnh sửa` navigates to the existing flashcard set edit page.
- The dropdown sorts the current full set client-side.
- The dropdown does not apply study settings such as starred-only or unlearned-only.
- The voice icon speaks the English term.
- The star icon reflects current starred state. If the current page already supports toggling stars from this context, reuse it; otherwise keep it display-only for this feature.

## Study Status

Add a richer progress state so sorting can distinguish learning states:

- `Status = 0`: `Chưa học`
- `Status = 1`: `Đang học`
- `Status = 2`: `Đã thành thạo`

Also track:

- `CorrectCount`
- `WrongCount`

Rules:

- No progress row means `Chưa học`.
- Marking a card as unknown sets `Status = Đang học` and increments `WrongCount`.
- Marking a card as known sets `Status = Đã thành thạo` and increments `CorrectCount`.
- Existing `IsLearned` can remain for compatibility, but new UI and sorting should prefer `Status`.

## Data Flow

Use the existing flashcard study page model as much as possible.

- Server loads all cards in original order.
- Server includes each card's progress status for the current user.
- Razor serializes the list for the existing page script.
- JavaScript sorts and renders the vocabulary panel without a new API endpoint.

This is intended for ordinary vocabulary sets, roughly up to 500 cards. Larger sets can be optimized later with pagination or server-side sorting.

## Error Handling

- Missing optional fields render as empty text, not placeholder noise.
- Missing progress defaults to `Chưa học`.
- Text-to-speech failure should not block the page.
- If edit URL cannot be generated, the button should be hidden rather than broken.

## Testing

- Build the project.
- Verify the study page loads with cards that have and do not have progress.
- Verify each dropdown sort order.
- Verify `Chỉnh sửa` opens the existing edit page.
- Verify vocabulary card voice plays the English term.
- Verify mobile layout stacks the top row and keeps card text readable.
