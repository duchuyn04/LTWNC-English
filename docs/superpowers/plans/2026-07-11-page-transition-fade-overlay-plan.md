# Page Transition — Fade Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a click-intercept, full-viewport fade-overlay page transition to the existing ASP.NET MVC app.

**Architecture:** A small standalone JS controller (`wwwroot/js/page-transition.js`) intercepts internal link clicks, fades a fixed overlay in before navigation, and fades it out on every page load. `_Layout.cshtml` renders the overlay, and `site.css` provides the transition styles. Existing GSAP scroll animations are left untouched.

**Tech Stack:** ASP.NET Core MVC, Razor, vanilla JS, CSS transitions.

## Global Constraints

- Respect `prefers-reduced-motion: reduce` (instant transition).
- Only intercept same-origin internal links; ignore external links, `target="_blank"`, `download`, modifier-key clicks, and hash-only links.
- Do not break no-JS users or back/forward cache behavior.
- Overlay background color: `#F7F6F3`.
- Fade-in duration: 350 ms; hold before navigation: ~150 ms; fade-out duration: 400 ms.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `Views/Shared/_Layout.cshtml` | Render the overlay element, include the transition script, provide no-JS fallback. |
| `wwwroot/css/site.css` | Overlay styles, keyframes, reduced-motion media query. |
| `wwwroot/js/page-transition.js` | Intercept clicks, drive fade-in/out, handle `pageshow` and `DOMContentLoaded`. |

---

### Task 1: Create the page transition controller

**Files:**
- Create: `wwwroot/js/page-transition.js`
- Modify: none
- Test: manual

**Interfaces:**
- Consumes: none (self-contained IIFE).
- Produces: module attaches event listeners on `DOMContentLoaded`, `pageshow`, and `click`.

- [ ] **Step 1: Write `wwwroot/js/page-transition.js`**

```javascript
(function () {
    const OVERLAY_ID = 'pt-overlay';
    const CLASS_VISIBLE = 'pt-overlay--visible';
    const CLASS_IN = 'pt-overlay--in';
    const CLASS_OUT = 'pt-overlay--out';

    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function getOverlay() {
        return document.getElementById(OVERLAY_ID);
    }

    function createOverlay() {
        let overlay = getOverlay();
        if (overlay) return overlay;
        overlay = document.createElement('div');
        overlay.id = OVERLAY_ID;
        overlay.className = CLASS_VISIBLE;
        document.body.appendChild(overlay);
        return overlay;
    }

    function fadeOut() {
        const overlay = getOverlay();
        if (!overlay) return;

        if (prefersReducedMotion()) {
            overlay.remove();
            return;
        }

        overlay.classList.add(CLASS_OUT);
        overlay.addEventListener('transitionend', () => overlay.remove(), { once: true });
        setTimeout(() => overlay.remove(), 600);
    }

    function fadeInAndNavigate(href) {
        const overlay = createOverlay();
        overlay.classList.remove(CLASS_VISIBLE);
        overlay.classList.remove(CLASS_OUT);

        if (prefersReducedMotion()) {
            location.href = href;
            return;
        }

        void overlay.offsetWidth;
        overlay.classList.add(CLASS_IN);
        setTimeout(() => {
            location.href = href;
        }, 500);
    }

    function shouldIntercept(anchor, event) {
        if (!anchor || !anchor.href) return false;
        if (anchor.hostname !== location.hostname) return false;
        if (event.button !== 0) return false;
        if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return false;
        if (anchor.target === '_blank' || anchor.hasAttribute('download')) return false;
        if (anchor.closest('[data-no-transition]')) return false;

        const href = anchor.getAttribute('href');
        if (!href || href.startsWith('#')) return false;
        if (href.startsWith('javascript:') || href.startsWith('mailto:') || href.startsWith('tel:')) return false;

        return true;
    }

    document.addEventListener('DOMContentLoaded', fadeOut);
    window.addEventListener('pageshow', fadeOut);

    document.addEventListener('click', (event) => {
        const anchor = event.target.closest('a[href]');
        if (!shouldIntercept(anchor, event)) return;
        event.preventDefault();
        fadeInAndNavigate(anchor.href);
    });
})();
```

- [ ] **Step 2: Commit**

```bash
git add wwwroot/js/page-transition.js
git commit -m "feat(page-transition): add click-intercept fade overlay controller"
```

---

### Task 2: Add overlay styles

**Files:**
- Create: none
- Modify: `wwwroot/css/site.css` (append to end of file)
- Test: manual

**Interfaces:**
- Consumes: element `#pt-overlay` rendered by `_Layout.cshtml`.
- Produces: CSS classes `.pt-overlay--visible`, `.pt-overlay--in`, `.pt-overlay--out` used by `page-transition.js`.

- [ ] **Step 1: Append overlay CSS to `wwwroot/css/site.css`**

```css
/* ==================== PAGE TRANSITION OVERLAY ==================== */

#pt-overlay {
    position: fixed;
    inset: 0;
    z-index: 9999;
    background: #F7F6F3;
    opacity: 0;
    pointer-events: none;
    transition: opacity 350ms ease-out;
}

#pt-overlay.pt-overlay--visible,
#pt-overlay.pt-overlay--in {
    opacity: 1;
    pointer-events: all;
}

#pt-overlay.pt-overlay--out {
    opacity: 0;
    pointer-events: none;
    transition-duration: 400ms;
}

@media (prefers-reduced-motion: reduce) {
    #pt-overlay {
        transition-duration: 0ms !important;
    }
}

/* No-JS fallback */
noscript #pt-overlay {
    display: none !important;
}
```

- [ ] **Step 2: Commit**

```bash
git add wwwroot/css/site.css
git commit -m "feat(page-transition): add overlay fade styles"
```

---

### Task 3: Render overlay and include script in layout

**Files:**
- Create: none
- Modify: `Views/Shared/_Layout.cshtml`
- Test: manual

**Interfaces:**
- Consumes: `wwwroot/js/page-transition.js` and CSS from `wwwroot/css/site.css`.
- Produces: `#pt-overlay` element visible on every page; script loaded before `</body>`.

- [ ] **Step 1: Add overlay markup right after the opening `<body>` tag**

```html
<body>
    <div id="pt-overlay" class="pt-overlay--visible"></div>
    <noscript>
        <style>
            #pt-overlay { display: none !important; }
        </style>
    </noscript>
```

- [ ] **Step 2: Add the transition script after `site.js`**

```html
    <script src="~/js/site.js" asp-append-version="true"></script>
    <script src="~/js/page-transition.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
```

- [ ] **Step 3: Commit**

```bash
git add Views/Shared/_Layout.cshtml
git commit -m "feat(page-transition): render overlay and load controller in layout"
```

---

### Task 4: Verify the transition

**Files:**
- Create: none
- Modify: none
- Test: manual

- [ ] **Step 1: Start the app**

```bash
dotnet run
```

- [ ] **Step 2: Basic transition check**
  - Open the app in a browser.
  - Click an internal link (e.g., **Trang chủ** → **Bộ thẻ của tôi**).
  - Expected: a light overlay fades in over the current page, then the new page fades in.

- [ ] **Step 3: Modifier-key check**
  - Ctrl/Cmd+click an internal link.
  - Expected: the link opens in a new tab; the current page does **not** fade out.

- [ ] **Step 4: External/hash link check**
  - If an external link exists, click it.
  - Click a same-page anchor link (e.g., `#top`).
  - Expected: both behave natively; no overlay.

- [ ] **Step 5: Back/forward check**
  - Navigate to a second page, then press the browser Back button.
  - Expected: the previous page loads and the overlay fades out cleanly.

- [ ] **Step 6: Reduced motion check**
  - Enable **prefers-reduced-motion** in the OS/browser.
  - Navigate between pages.
  - Expected: transition is instant; no visible animation.

- [ ] **Step 7: No-JS check**
  - Disable JavaScript in the browser.
  - Reload any page.
  - Expected: content is visible; overlay does not block the page.

- [ ] **Step 8: Commit verification notes (optional)**

If any check fails, fix in the relevant task file and re-run the failed check before continuing.

---

## Spec Coverage

| Spec Requirement | Task |
|------------------|------|
| Fade overlay on internal navigation | Task 1 + Task 3 |
| Overlay background `#F7F6F3` | Task 2 |
| Timing: 350 ms fade-in, ~150 ms hold, 400 ms fade-out | Task 1 (`setTimeout(500)`) + Task 2 (`transition` durations) |
| Respect `prefers-reduced-motion: reduce` | Task 1 (`prefersReducedMotion`) + Task 2 (`@media`) |
| Ignore external links / modifier keys / hash-only / downloads / `target="_blank"` | Task 1 (`shouldIntercept`) |
| No-JS fallback | Task 3 (`<noscript>`) + Task 2 (`noscript #pt-overlay`) |
| Back/forward cache behavior | Task 1 (`pageshow` listener) |
| Existing GSAP animations untouched | No changes to `site.js` GSAP code |

## Placeholder Scan

- No `TBD`, `TODO`, or vague steps.
- All code blocks contain complete, copy-pasteable code.
- All file paths are exact.
- No "similar to Task N" references.

## Type Consistency

- CSS classes referenced in `page-transition.js` match the selectors in `site.css`:
  - `.pt-overlay--visible`
  - `.pt-overlay--in`
  - `.pt-overlay--out`
- Element ID referenced in JS matches markup in `_Layout.cshtml`: `#pt-overlay`.
- Script path referenced in `_Layout.cshtml` matches created file: `~/js/page-transition.js`.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-07-11-page-transition-fade-overlay-plan.md`. Two execution options:**

1. **Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints.

**Which approach?**
