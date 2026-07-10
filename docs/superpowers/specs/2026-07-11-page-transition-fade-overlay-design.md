# Page Transition — Fade Overlay

**Date:** 2026-07-11  
**Status:** Approved  
**Chosen variant:** A — Fade overlay  
**Implementation approach:** Click-intercept with full-viewport overlay

## Problem

Current ASP.NET MVC app uses GSAP entrance animations on scroll (`wwwroot/js/site.js`). There is no real page-transition animation, so navigating between server-rendered pages causes a visible flash/white gap and then the new content jumps in. The user perceived this as “jerky.”

## Goal

Add a smooth, unobtrusive fade-overlay transition for internal page navigation that:

- hides the flash between page loads,
- feels premium but stays out of the way of studying,
- works with the existing server-rendered MVC architecture,
- respects `prefers-reduced-motion: reduce`.

## Decision log

| Option | Verdict | Reason |
|--------|---------|--------|
| A. Fade overlay | **Chosen** | Mượt, ít gây chói, phù hợp web học tập. |
| B. Slide ngang | Rejected | Cần nhiều không gian ngang, dễ gây mệt khi chuyển trang liên tục. |
| C. Scale soft | Rejected | Sang nhưng hơi “nặng” cho app học tập. |
| D. Minimal fade-in | Rejected | Chỉ fix flash, không có cảm giác “trang cũ mờ đi”. |

Implementation approach: **click-intercept + overlay fade** instead of load-only fade-in, because the user explicitly wants the “old page fades out” feel.

## User flow

1. User clicks an internal link.
2. Current page stays visible while a full-viewport overlay fades in (opacity 0 → 1).
3. Browser navigates to the new URL.
4. New page renders with the overlay already visible.
5. Overlay fades out (opacity 1 → 0), revealing the new content.

Total perceived transition time: ~500–700 ms.

## Architecture

```
Views/Shared/_Layout.cshtml
├── #pt-overlay element (rendered on every page)
└── inline no-JS fallback

wwwroot/css/site.css
├── .pt-overlay
├── .pt-overlay--in
├── .pt-overlay--out
├── .pt-overlay--visible
└── @media (prefers-reduced-motion: reduce)

wwwroot/js/site.js
└── pageTransition module
    ├── on DOMContentLoaded: fade overlay out
    ├── on internal link click: fade overlay in, then navigate
    └── on pageshow: ensure overlay is hidden after back/forward cache
```

## Detailed behavior

### Overlay element

- Fixed, full viewport, `z-index: 9999`.
- Background: `#F7F6F3` (app canvas color) so it blends with the surrounding page.
- `pointer-events: all` while visible to prevent interaction during transition.

### Click interception rules

Only intercept a click when **all** are true:

- Target is an `<a>` with `href`.
- Same origin (`a.hostname === location.hostname`).
- `href` is not only a hash on the current page.
- No modifier key (Ctrl, Cmd, Shift, Alt).
- No `target="_blank"`, `download`, or `data-no-transition` attribute.

Everything else (external links, file downloads, new-tab clicks, forms) keeps native behavior.

### Animation timing (default)

- Fade-in duration (old page): 250 ms, `ease-out`.
- Hold at full opacity: ~100 ms before navigation.
- Fade-out duration (new page): 350 ms, `ease-out`.

If `prefers-reduced-motion: reduce` is active, durations collapse to 0 ms (instant).

### Page-load handling

- `_Layout` renders `#pt-overlay` with class `.pt-overlay--visible`.
- On `DOMContentLoaded`, the script adds `.pt-overlay--out`, then removes the element from the DOM after the animation ends.
- On `pageshow`, the script ensures the overlay is hidden (covers back/forward cache restore).

### No-JS fallback

- A `<noscript>` style sets `body { opacity: 1 !important; }` and hides the overlay.
- If JS fails, the overlay must not block content permanently.

## Files changed

1. `Views/Shared/_Layout.cshtml` — add overlay markup + `noscript` fallback.
2. `wwwroot/css/site.css` — add overlay styles and keyframes.
3. `wwwroot/js/site.js` — add transition controller. Keep existing GSAP entrance animations untouched.

## Accessibility

- Respect `prefers-reduced-motion: reduce`.
- Do not intercept keyboard navigation in a way that breaks screen-reader expectations; focus management is left to the browser after navigation.
- No overlay traps focus permanently because it is removed after transition.

## Risks & mitigation

| Risk | Mitigation |
|------|------------|
| Overlay blocks page if JS fails | `noscript` fallback hides overlay when JS is disabled; JS also removes a stuck overlay on `pageshow` |
| Back/forward looks jumpy | Handle `pageshow` event |
| Middle-click / Ctrl+click broken | Check modifier keys and ignore them |
| External links accidentally intercepted | Only intercept same-origin links |
| GSAP entrance animations conflict | Keep modules independent; overlay runs on `body`, GSAP runs on content elements |

## Out of scope

- SPA routing / Turbo / Hotwire.
- Transition on form POST submissions.
- Custom per-page transition variants.

## Acceptance criteria

- [ ] Clicking any internal link in the navbar shows the overlay fade before navigation.
- [ ] New page fades in smoothly after load.
- [ ] Ctrl/Cmd+click, Shift+click, and middle-click still open in a new tab/window.
- [ ] External links, `download` links, and hash-only links are unaffected.
- [ ] With `prefers-reduced-motion: reduce`, transitions are instant.
- [ ] Back/forward navigation does not leave the overlay stuck.
- [ ] Page is still usable if JavaScript is disabled.

## Next step

Invoke `superpowers:writing-plans` to create the implementation plan.
