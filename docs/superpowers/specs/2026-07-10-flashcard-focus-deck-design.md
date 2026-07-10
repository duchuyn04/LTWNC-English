# Flashcard Focus Deck

## Goal

Make the flashcard study screen faster to use and less distracting. A learner should see one card, understand its state, and move to the next action without scanning several separate control bands.

## Scope

- Update only `Views/Study/Flashcard.cshtml` and `wwwroot/css/flashcard.css`.
- Preserve existing routes, study settings, flip behavior, TTS, star toggle, progress marking, shuffle, and keyboard shortcuts.
- Use the existing Bootstrap 5, Material Symbols, and warm-neutral token system.
- Do not add dependencies, backend work, or new learning modes.

## Information hierarchy

1. Compact header: exit action, set title, active filter, and progress.
2. A slim learning-mode strip that keeps the active mode obvious without competing with the card.
3. Flashcard: the word or definition is the visual focus; one flip hint is shown.
4. One action rail immediately below the card: progress mode, previous, position, next, shuffle, and voice settings.
5. Vocabulary becomes a collapsed supporting section. It remains available without occupying the initial learning viewport.

## Layout

### Header and modes

- Reduce the vertical gap between header, mode strip, and card.
- Keep the seven existing mode buttons, but render them as compact segments on desktop and a two-column grid on mobile.
- Keep unavailable modes disabled and keep the active `Học` mode visually dominant.

### Flashcard

- Use a focused card height of 420px on desktop, shrinking responsively on smaller screens.
- Remove duplicate flip instructions from the top and bottom lines; retain the central `Click hoặc Space để lật` hint.
- Retain settings, star, badge, autoplay toggle, and audio controls in their existing locations.
- Use a smaller, softer frame shadow and a tighter outer frame so the card reads as a single surface instead of nested empty rectangles.

### Action rail

- Put the card navigation controls and voice settings in one compact rail beneath the card.
- Keep the existing control IDs and click handlers so keyboard behavior and progress mode remain unchanged.
- Remove the duplicate shortcut strip. Keyboard shortcuts remain discoverable through concise button titles and the flip hint.

### Vocabulary support area

- Wrap the existing vocabulary toolbar and grouped rows in a native `details` section.
- The summary shows `Từ trong bộ` and opens the existing toolbar and rows when requested.
- Preserve sorting, audio buttons, starred display, grouping, and the edit link.

## Accessibility and interaction

- Keep all controls as semantic links, buttons, form controls, or `details`/`summary`.
- Preserve visible focus states and existing keyboard shortcuts: Space, arrows, 1, 2, and Backspace.
- Keep tap targets at least 40px for primary icon controls.
- Respect reduced motion by disabling card/frame transitions when `prefers-reduced-motion: reduce` is active.

## Verification

- `dotnet build` completes with zero warnings and errors.
- At 375px, the study view has no horizontal overflow and the card is readable without hiding its primary controls.
- At desktop width, all seven mode buttons fit on one row and the action rail stays adjacent to the card.
- Verify Space flips, arrows navigate, 1/2 mark progress, Backspace exits, vocabulary sorting works after opening the section, and the voice settings remain reachable.
