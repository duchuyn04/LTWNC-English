# Flashcard Study Mode Prototype Design

**Date:** 2026-07-23  
**Status:** Approved for prototype

## Design question

How should all available learning modes be exposed directly on the Flashcard screen so learners no longer need the separate Study Hub mode-selection page?

## Current behavior

- `/Study/{setId}` renders a separate Study Hub with progress, filters, and learning-mode cards.
- `/Study/{setId}/Flashcard` renders the actual Flashcard workspace.
- The Flashcard workspace currently exposes Flashcard, Dictation, and English Mission tabs, but does not expose Quiz.
- Set-level “Học” links lead to `/Study/{setId}`, adding an intermediate choice before learning begins.

## Validated direction

- Flashcard becomes the default learning entry for a Set.
- The Flashcard screen exposes all four implemented modes:
  - Flashcard
  - Nghe chép
  - Kiểm tra
  - English Mission
- The existing Flashcard content, progress, filters, settings, and vocabulary list remain available.
- The prototype evaluates navigation hierarchy and placement only. It does not add new persistence or backend mutations.

## Prototype shape

Use the existing Flashcard route as a sub-shape A UI prototype:

`/Study/{setId}/Flashcard?variant=A`

The existing controller data and page content remain the source of truth. Only the presentation of the four learning-mode actions changes. The prototype is available only in the Development environment.

Three structurally distinct variants are switchable through `?variant=A|B|C`:

### Variant A — Horizontal mode bar

- Place four persistent mode tabs below the Set title.
- Keep the active Flashcard mode visually dominant.
- Show unavailable modes as disabled items with their existing reason.
- Optimize for immediate recognition and one-click switching.

This is the recommended starting point because it is familiar, compact, and does not compete with the flashcard itself.

### Variant B — Left mode rail

- Place the four modes in a narrow vertical rail beside the flashcard workspace.
- Use icon, label, and active state for each mode.
- Collapse the rail into a horizontal or compact control on narrow screens.
- Optimize for a workspace-like desktop experience and persistent navigation.

### Variant C — Compact mode launcher

- Keep the Flashcard canvas visually quiet.
- Place a “Chế độ học” launcher near the progress/header controls.
- Open a popover containing four mode choices, short descriptions, and availability.
- Optimize for maximum flashcard focus at the cost of one additional interaction.

## Prototype switcher

- A fixed, high-contrast bar appears at the bottom center in Development only.
- Left and right controls cycle through A, B, and C with wraparound.
- The current variant key and name are visible.
- `ArrowLeft` and `ArrowRight` cycle variants unless focus is inside an input, textarea, select, button, link, or editable element.
- Switching updates the URL search parameter so a variant remains shareable and stable on reload.

## Navigation behavior represented by the prototype

- The Flashcard item remains active and does not navigate away.
- Dictation links to `/Study/{setId}/Dictation`.
- Quiz links to `/Study/{setId}/Quiz`.
- English Mission links to `/Study/{setId}/Mission`.
- Unavailable modes remain visible but non-interactive and communicate the existing unavailable reason.
- No prototype control submits forms or changes study progress.

## Responsive expectations

- Desktop variants must preserve the flashcard as the primary visual focus.
- At tablet widths, no variant may cause horizontal overflow.
- On mobile, mode navigation must remain reachable before or near the flashcard without hiding learning controls.
- The prototype switcher may overlap neither primary flashcard controls nor browser-safe bottom space.

## Success criteria

The prototype answers the design question when the user can compare the three variants and choose:

- the clearest way to discover all four learning modes;
- the best balance between mode navigation and flashcard focus;
- any components to combine across variants.

## Out of scope

- Removing the Study Hub production route.
- Redirecting Set “Học” links.
- Rewriting the selected prototype as production code.
- Changing mode availability rules, session creation, filters, or persistence.
- Adding automated tests to the throwaway prototype.

After a winner is selected, its decision will be implemented cleanly in production, and the full prototype will be captured on a throwaway branch rather than retained on `master`.
