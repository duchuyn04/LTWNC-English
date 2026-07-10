# Flashcard Commercial Redesign

Date: 2026-07-10

## Goal

Redesign the entire flashcard flow (study page, set list, create/edit, set details) so the UI/UX feels polished, trustworthy, and ready for commercialization. The current interface is functional but looks rough and prototype-like; the redesign must elevate it to a product users would pay for.

## Design Direction

**Polished Productivity + Warm.** A clean, organized interface built on warm neutrals with a single amber accent. The feel is professional enough to justify a subscription or one-time purchase, yet approachable for a mass-market audience of language learners.

Inspiration: Notion/Linear for structure and clarity, with the warmth of Headspace and the friendly efficiency of Duolingo's calmer moments.

## Target Audience

Mass-market language learners across ages:
- Students and self-learners
- Working professionals studying on the side
- Casual learners who value clarity over novelty

Not targeting children specifically, but the warm palette keeps it accessible.

## Visual Design System

### Color Palette

| Token | Hex | Usage |
|-------|-----|-------|
| Background | `#fafaf9` | Page background |
| Surface | `#ffffff` | Cards, panels, dialogs |
| Primary Text | `#292524` | Headings, key labels |
| Secondary Text | `#78716c` | Captions, meta, placeholders |
| Muted | `#a8a29e` | Disabled, subtle hints |
| Accent | `#f59e0b` | Primary actions, progress, active states |
| Success | `#10b981` | Known/mastered, positive feedback |
| Error | `#ef4444` | Unknown, errors, destructive actions |
| Border | `#e7e5e4` | Card borders, dividers, input borders |
| Soft BG | `#f5f5f4` | Secondary buttons, badges, hover states |
| Warm Accent BG | `#fff7ed` | Accent backgrounds, highlights |

### Typography

- **Font family:** Be Vietnam Pro (already loaded in `_Layout.cshtml`).
- **Heading Large:** 2rem, weight 800, letter-spacing -0.02em, color primary text.
- **Heading Medium:** 1.25rem, weight 700, color primary text.
- **Heading Small:** 1rem, weight 600, color primary text.
- **Body:** 1rem, weight 400, line-height 1.6, color secondary text.
- **Caption/Meta:** 0.875rem, weight 400 or 600, color secondary/muted.
- **Badges:** 0.75rem, weight 700, uppercase, letter-spacing 0.05em.

### Shapes & Elevation

- **Border radius scale:**
  - Pills / badges: 999px
  - Cards / panels: 16px
  - Buttons / inputs: 10px
  - Small tags / chips: 8px
- **Shadows:** subtle, only on cards above background.
  - Default card: `0 2px 8px rgba(0,0,0,0.04)`
  - Hover/elevated: `0 8px 32px rgba(0,0,0,0.06)`
- **Borders:** 1px `#e7e5e4` for cards and inputs.

### Components

#### Buttons

- **Primary:** dark background (`#292524`), white text, rounded 10px, weight 600.
- **Secondary:** white background, dark border, dark text.
- **Accent:** warm amber background (`#fff7ed`), amber text, for highlights.
- **Ghost icon buttons:** 36-44px square, rounded 8-12px, border 1px `#e7e5e4`.

#### Cards

White surface, 16px radius, 1px border, subtle shadow. Used for flashcard frame, set cards, vocab rows, and detail summary.

#### Badges

Small uppercase pills with soft background. Examples: `ENGLISH`, `NGHĨA VIỆT`, `CHƯA HỌC`.

#### Inputs

Light background or white, 1px border, 10px radius, comfortable padding. Focus ring in accent color.

## Page-by-Page Redesign

### 1. Study Page

The study page is the hero surface. It must feel focused and rewarding.

#### Header

- Left: rounded "Thoát" button with back arrow.
- Center: eyebrow label "HỌC FLASHCARD" + set title.
- Right: progress text "Thẻ X / Y" + thin amber progress bar.

#### Learning Mode Pills

A row of mode chips below the header (Speaking, Hội thoại, Ngữ pháp, Đọc hiểu, Học, Kiểm tra, Nghe Chép). Currently only "Học" is active; others are disabled placeholders that hint at future modules.

#### Flashcard

- Large white card (max ~600px) with generous padding and rounded corners.
- Front shows term + IPA prominently.
- Top-left: settings gear icon.
- Top-right: language badge + star toggle.
- Bottom: audio controls and auto-play toggle.
- Subtle hint text: "Click hoặc Space để lật".
- Back shows definition, example, and optional image.
- 3D flip animation is preserved but refined with smoother easing.

#### Navigation

- Centered control row: Tiến độ button, prev/next arrows, card counter, shuffle.
- In "progress mode", left/right become "Chưa biết" (red) and "Đã biết" (green).
- Keyboard shortcuts remain: Space to flip, arrows to navigate, 1/2 to mark, Backspace to exit.

#### Vocabulary Panel

- White panel below the card.
- Toolbar: "Chỉnh sửa" link + sort dropdown.
- Grouped by status: "Chưa học" and "Đã thành thạo".
- Each row: term + IPA | definition + example | audio button.
- Star state is display-only in the list.

#### Completion Screen

- Centered card with fireworks canvas.
- Stats: "Đã biết" (green) and "Cần ôn" (red).
- Actions: "Ôn lại" and "Về trang học".

### 2. Set List Page

- Grid of set cards (responsive 1-3 columns).
- Each card shows:
  - Set title
  - Word count + progress percentage
  - Progress bar
  - "Học" primary button + "Sửa" secondary button
  - Overflow menu for delete/share (future)
- Empty state: friendly illustration + "Tạo bộ thẻ đầu tiên" CTA.

### 3. Create / Edit Set Page

- Clean single-column form.
- Set title input at top.
- "Lưu bộ thẻ" sticky or prominent primary action.
- Card list as editable rows:
  - Front input | Back input | image upload icon | delete icon
  - "+ Thêm thẻ" accent button below list
- Auto-expand new row when clicking add.

### 4. Set Details Page

- Hero summary card with:
  - Title
  - Meta: word count, author, last updated
  - Stats: progress %, known count, review count
  - "Bắt đầu học" primary CTA
- Below: scrollable word list with edit button.
- Optional future: study history graph, streak info.

## UX Improvements

1. **Clearer hierarchy.** Reduce competing controls on the study page; the flashcard itself is the hero.
2. **Consistent feedback.** Buttons have hover/active states. Cards lift slightly on hover.
3. **Readable progress.** Progress bars use accent/success colors and are always visible.
4. **Friendly empty states.** No blank screens; every empty list has a guided next step.
5. **Reduced cognitive load.** Settings panel stays compact; mode pills set context without adding noise.
6. **Mobile-first stack.** All layouts collapse cleanly on small screens.

## Interactions & Motion

- Card flip: 520ms cubic-bezier easing with 3D perspective.
- Button hover: slight lift + color shift, 180ms.
- Card hover: subtle shadow increase, 220ms.
- Progress bar: 260ms width transition.
- Page transitions: optional fade-in for completion screen.

## Implementation Notes

- Keep using Bootstrap 5 for layout grid and utilities.
- Replace custom Phosphor icons on study page with Material Symbols for consistency with the new design.
- Update `wwwroot/css/flashcard.css` to reflect the new palette and component styles.
- Preserve existing JavaScript logic: flip, navigation, star toggle, TTS, settings, progress marking, fireworks.
- Update `renderVocabularyPanel()` to group cards by status and render the new row layout.
- Ensure `FlashcardStudyViewModel` and existing endpoints remain compatible.

## Out of Scope

- New backend features beyond styling and minor JS reorganization.
- New learning modes (Speaking, Kiểm tra, etc.) — mode pills are placeholders.
- Mobile native app.
- Advanced analytics or social features.
- Payment/subscription integration.

## Verification

- `dotnet build` passes.
- Study page renders correctly with cards, images, settings, and vocab panel.
- Flip animation works on desktop and mobile.
- Keyboard shortcuts work.
- Set list, create/edit, and details pages render with new styles.
- Empty states are visible and styled.
- Mobile layout does not break.