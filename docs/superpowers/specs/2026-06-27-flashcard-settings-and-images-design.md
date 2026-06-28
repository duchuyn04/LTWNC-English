# Flashcard Settings and Images Design

Date: 2026-06-27

## Goal

Add a compact settings panel to the flashcard study screen, positioned at the top-left of the flashcard. Add image support to vocabulary cards so study settings can show, hide, blur, and size card images.

## Decisions

- Use the compact popover layout selected as option A.
- Add image support to cards using both URL and upload.
- Persist study settings per logged-in user in the database.
- Anonymous users can change settings only for the current page session.
- Define "unlearned" as cards with no `UserProgress` row or `IsLearned == false`.
- Default study mode is rich vocabulary:
  - Front: term and IPA.
  - Back: definition, example, and small image.
  - Front pronunciation enabled.
- Upload validation: JPG, PNG, or WebP only, max 2 MB.

## Data Model

Extend `Flashcard` with:

- `ImageUrl`: optional external image URL.
- `UploadedImagePath`: optional local image path under `wwwroot/uploads/flashcards`.

Render priority:

1. Use `UploadedImagePath` when present.
2. Else use `ImageUrl`.
3. Else render no image.

Add one user-level study settings record keyed by `UserId`. Keep it small: store current flashcard study preferences only. No presets, per-set overrides, or new dependency.

Settings include:

- `StarredOnly`
- `UnlearnedOnly`
- front-side visibility: term, definition, IPA, image
- back-side visibility: term, definition, IPA, example, image
- image display: hidden, blurred, small or large
- pronunciation: front, back

## Card Edit UI

The card create/edit form adds:

- `ImageUrl` text input.
- Native file input for image upload.
- Current image preview when an image exists.
- Remove uploaded image action.

If user provides both upload and URL, uploaded image wins. URL remains available as fallback if upload is removed.

## Study UI

The flashcard study page adds one icon button at the top-left inside the flashcard frame. Clicking it opens a popover.

Popover groups:

- Vocabulary filter: starred only, unlearned only.
- Front side: term, definition, IPA, image.
- Back side: term, definition, IPA, example, image.
- Image: hidden, blurred, small, large.
- Pronunciation: front, back.

Desktop uses a floating popover under the button. Mobile uses the same content constrained to viewport width so it does not overflow.

## Behavior

Changing filters refreshes the current study list:

- `StarredOnly` filters to starred cards.
- `UnlearnedOnly` filters to cards not yet learned.
- Both enabled means intersection of both filters.

Changing display settings updates the current page immediately. Logged-in users persist settings through a small AJAX endpoint. Anonymous users keep settings in memory for the current page only.

Empty card guard:

- A card side may hide all text only if an image is visible for that side.
- If a side has no visible image, UI keeps the last visible text option enabled.

## Error Handling

- Upload with wrong type or size over 2 MB returns a form error and does not save the card change.
- Empty image URL is treated as no URL.
- External image URL is not server-fetched or validated.
- Settings save failure does not block study; UI keeps current page state and reload falls back to saved/default settings.
- Empty filtered study list redirects to study index with a message.

## Verification

- `dotnet build`
- Upload valid JPG/PNG/WebP image.
- Reject wrong type and file over 2 MB.
- Card with upload image renders image in study.
- Card with only URL renders image in study.
- Uploaded image wins when both upload and URL exist.
- Starred and unlearned filters work alone and together.
- Marking a card learned removes it from unlearned-only after reload.
- Logged-in user settings persist after reload.
- Anonymous public study route works and does not persist settings.

## Out of Scope

- Image cropper.
- Client-side compression.
- CDN or remote image proxy.
- Multiple images per card.
- Study presets.
- Per-set setting overrides.
