# Flashcard Study UI/UX Redesign

## Goal

Redesign the flashcard study screen (`/Study/{setId}/Flashcard`) to a cleaner, more focused, and more polished UI/UX while preserving all existing functionality.

## Project Context

- ASP.NET Core MVC application using Razor views.
- Current view: `Views/Study/Flashcard.cshtml`.
- Current styles: `wwwroot/css/flashcard.css`.
- Current behavior is driven by inline JavaScript in `Flashcard.cshtml`.
- Existing features that must be preserved:
  - 3D card flip (front/back)
  - Star/unstar card
  - Text-to-speech with voice/speed selection
  - Progress tracking and "learned/unlearned" marking
  - Keyboard shortcuts (Space, ←, →, 1, 2, Backspace, Ctrl)
  - Filter by "Tất cả" / "Đã sao"
  - Vocabulary panel with sort/group
  - Completion screen with fireworks
  - Study settings (visible fields per side, image options)

## Design Direction

- **Visual style:** Minimal & Clean
- **Experience priority:** Focus Mode
- **Control placement:** Fixed control bar below the card
- **Secondary panels:** Collapsed by default, expandable when needed

## Design Decisions

### Layout & Visual Hierarchy

The screen is reorganized into a single vertical reading flow that keeps the flashcard as the dominant element:

1. **Top bar** — exit pill on the left, set title + progress in the center, user menu on the right (reuse existing site layout).
2. **Progress strip** — a thin full-width progress bar directly under the header. Card counter is embedded in or adjacent to the bar.
3. **Card area** — large centered card, max-width `720px`, generous vertical spacing.
4. **Control bar** — directly below the card: prev, flip, next. When the card is flipped, the bar switches to: prev, "Chưa biết", "Đã biết", next.
5. **Collapsed sections** — learning-mode chips and vocabulary panel are collapsed into accordion bars at the bottom, closed by default.

### Color & Typography

- **Palette**
  - Background: `#fafaf9` (warm stone-50)
  - Card surface: `#ffffff`
  - Primary text: `#1c1917` (stone-900)
  - Secondary text: `#78716c` (stone-500)
  - Accent: `#f59e0b` (amber-500) for active states, stars, progress
  - Success/error: `#10b981` / `#ef4444` used as soft backgrounds for rating buttons
- **Typography**
  - Font stack: Inter / system-ui (project default)
  - Front term: `clamp(2.5rem, 7vw, 5rem)`, weight 700
  - Back meaning: `clamp(2rem, 5vw, 3.5rem)`, weight 700
  - IPA / example: `1rem`, weight 400, secondary color
  - Limit to 3–4 distinct font sizes for clarity

### Components

#### Header

- Remove the "Học flashcard" eyebrow badge; keep only the set title as the H1.
- "Thoát" button becomes a compact pill with just an icon and short label.
- Filter links "Tất cả / Đã sao" move into the settings drawer/dropdown to reduce header noise.

#### Flashcard

- Border: `1px solid #e7e5e4`, border-radius `24px`.
- Shadow: `0 20px 50px rgba(0,0,0,0.06)`.
- Star button in the top-right corner; shows filled state on hover/active.
- Small badge "English" / "Nghĩa Việt" in the top-left corner.
- Audio button in the bottom-right corner; autoplay toggle moved into settings drawer.

#### Control Bar

- Prev / Next: circular `48px` buttons with arrow icons.
- Flip: centered, more prominent filled button.
- On flip, Flip morphs into two rating buttons:
  - "Chưa biết" — soft red background, left side
  - "Đã biết" — soft green background, right side
- Small keyboard hints below the bar.

#### Collapsed Sections (Accordions)

- **Chế độ học** — closed by default; opens to show the existing learning-mode chips (most still disabled).
- **Từ trong bộ (X từ)** — closed by default; opens to show the existing grouped vocabulary list.

#### Settings Drawer

- Slide-in panel from the right containing:
  - Filter: "Tất cả / Đã sao / Chưa thuộc"
  - TTS: voice select, speed select, autoplay toggle
  - Card face options: visible fields for front/back
  - Image options: hide, blur, large

### Interactions & Animations

- **Card flip:** keep `rotateY` 3D flip, update easing to `cubic-bezier(0.34, 1.56, 0.64, 1)` for a slight physical overshoot.
- **Card transition on next/prev:** add `translateX(±20px)` + opacity fade so users perceive a card change.
- **Progress bar:** animate width over `300ms` when the index changes.
- **Control bar state change:** crossfade/scale between "flip" and "rating" modes instead of an instant swap.
- **Star toggle:** scale to `1.2` and switch to amber color.
- **Accordions:** smooth `height` transition over `250ms`.
- **Keyboard shortcuts:** preserve all existing shortcuts and add small inline hints.

### Responsive Behavior

- **Desktop (≥1024px):** card max-width `720px`, full horizontal control bar, accordions can use two columns when open.
- **Tablet (768–1023px):** card max-width `600px`, control bar remains horizontal, card font sizes slightly reduced.
- **Mobile (<768px):**
  - Header: exit button icon-only, set title truncated with ellipsis.
  - Card: nearly full width (`margin: 16px`), border-radius `20px`, height auto with `min-height: 280px`.
  - Control bar: prev/next at the outer edges, flip in the center.
  - When flipped, "Chưa biết / Đã biết" appear as a second row below the primary controls to avoid crowding.
  - Accordions: full width, single column.

## Out of Scope

- No changes to backend endpoints or data models.
- No new study modes (Speaking, Grammar, etc.); disabled chips remain disabled.
- No dark mode added in this iteration.

## Success Criteria

- The flashcard screen feels less cluttered and the card is visually dominant.
- All existing functionality continues to work unchanged.
- Keyboard shortcuts remain functional.
- Layout is comfortable on desktop, tablet, and mobile.
- Animations are smooth and do not block interaction.
