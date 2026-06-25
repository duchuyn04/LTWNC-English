# Landing Page Design Spec

## Overview

Transform the current simple home page into an engaging landing page with scroll animations, typewriter effects, and animated counters. Uses AOS library for scroll-triggered animations.

**Goal:** Create a visually appealing landing page that吸引 users to register and learn flashcards.

**Tech Stack:** AOS (Animate On Scroll), vanilla JS, existing Bootstrap 5 + Phosphor Icons.

---

## Layout

```
┌─────────────────────────────────┐
│         NAVBAR (sticky)         │
├─────────────────────────────────┤
│     1. HERO SECTION             │
│     Gradient bg + SVG           │
│     Typewriter title            │
│     CTA buttons                 │
├─────────────────────────────────┤
│     2. STATS COUNTER            │
│     Animated number counters    │
│     Users | Sets | Cards        │
├─────────────────────────────────┤
│     3. HOW IT WORKS             │
│     3 steps with icons          │
│     Fade-in stagger             │
├─────────────────────────────────┤
│     4. FEATURED SETS            │
│     Grid of public sets         │
│     Stagger animation           │
├─────────────────────────────────┤
│     5. FOOTER                   │
└─────────────────────────────────┘
```

---

## Section Details

### 1. Hero Section

**Background:** Gradient `#F7F6F3` → `#E1F3FE` (pale blue), subtle radial glow

**Content:**
- **Title:** "Học tiếng Anh với Flashcard" — typewriter effect (80ms per char)
- **Subtitle:** "Tạo bộ thẻ của riêng bạn hoặc học từ bộ thẻ có sẵn từ cộng đồng." — fade-up after title completes
- **CTA Buttons:**
  - Primary: "Bắt đầu học" → `/Account/Register` (or `/Set` if logged in)
  - Secondary: "Khám phá bộ thẻ" → smooth scroll to Featured Sets
- **Illustration:** SVG flashcard mockup (3 floating cards with slight rotation)

**Animations:**
- Title: Custom typewriter JS
- Subtitle: `data-aos="fade-up" data-aos-delay="1500"`
- Buttons: `data-aos="fade-up" data-aos-delay="2000"`
- Cards: CSS `@keyframes float` (infinite, 6s, ease-in-out)

### 2. Stats Counter

**Layout:** 3 equal columns on white card

**Stats:**
- `1,000+` Người dùng
- `500+` Bộ thẻ
- `10,000+` Thẻ đã học

**Animation:** Counter counts from 0 → target in 2s using `requestAnimationFrame`. Triggers when section enters viewport via IntersectionObserver.

**Design:**
- White card: `border: 1px solid #EAEAEA`, `border-radius: 12px`
- Numbers: `font-size: 2.5rem`, `font-weight: 700`, color `#111111`
- Labels: `font-size: 0.875rem`, color `#787774`

### 3. How It Works

**Layout:** 3 columns (stacked on mobile)

**Steps:**
1. **Tạo bộ thẻ** — Icon: `ph-plus-circle` — "Tạo bộ thẻ flashcard với từ vựng bạn muốn học"
2. **Học mỗi ngày** — Icon: `ph-brain` — "Lặp lại và ghi nhớ với hệ thống spaced repetition"
3. **Chia sẻ cộng đồng** — Icon: `ph-share-network` — "Chia sẻ bộ thẻ và học từ người khác"

**Animation:** Each step fades in from right with stagger delay (200ms between items)

**Design:**
- Icon: `font-size: 2rem`, color `#1F6C9F`, background `#E1F3FE`, `border-radius: 12px`, padding `16px`
- Title: `font-weight: 600`
- Description: color `#787774`

### 4. Featured Sets

**Layout:** 2x3 grid (responsive)

**Content:** 6 most recent public flashcard sets from database

**Each card:**
- Title
- Description (truncated)
- Card count badge
- "Học ngay" button

**Animation:** Stagger fade-in-up, 100ms delay between cards

**Design:** Same as existing `.card-custom` style

---

## Animations & Library

### AOS (Animate On Scroll)
- CDN CSS: `https://unpkg.com/aos@2.3.4/dist/aos.css`
- CDN JS: `https://unpkg.com/aos@2.3.4/dist/aos.js`
- Init: `AOS.init({ duration: 800, once: true })`

### Custom JS

**Typewriter Effect:**
```javascript
function typeWriter(element, text, speed = 80) {
    let i = 0;
    element.textContent = '';
    function type() {
        if (i < text.length) {
            element.textContent += text.charAt(i);
            i++;
            setTimeout(type, speed);
        }
    }
    type();
}
```

**Counter Animation:**
```javascript
function animateCounter(element, target, duration = 2000) {
    let start = 0;
    const startTime = performance.now();
    function update(currentTime) {
        const elapsed = currentTime - startTime;
        const progress = Math.min(elapsed / duration, 1);
        element.textContent = Math.floor(progress * target).toLocaleString();
        if (progress < 1) requestAnimationFrame(update);
        else element.textContent = target.toLocaleString() + '+';
    }
    requestAnimationFrame(update);
}
```

**Float Animation (CSS):**
```css
@keyframes float {
    0%, 100% { transform: translateY(0) rotate(-2deg); }
    50% { transform: translateY(-20px) rotate(2deg); }
}
```

---

## Files to Modify

| File | Changes |
|------|---------|
| `Views/Home/Index.cshtml` | Complete rewrite with 4 sections |
| `wwwroot/css/site.css` | Add landing page styles, gradient, float animation |
| `wwwroot/js/site.js` | Add typewriter, counter, AOS init |

---

## Responsive Behavior

| Breakpoint | Layout |
|------------|--------|
| Desktop (>992px) | Hero: text left + illustration right. Stats: 3 cols. How it works: 3 cols. |
| Tablet (768-992px) | Hero: centered. Stats: 3 cols. How it works: 3 cols. |
| Mobile (<768px) | Hero: centered. Stats: stacked. How it works: stacked. Featured: 1 col. |
