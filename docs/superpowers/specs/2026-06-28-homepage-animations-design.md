# Specification: Homepage Animation & Fog Redesign

## Goal

Add professional, high-end animations to the homepage (`Views/Home/Index.cshtml`) of LTWNC English to elevate its premium aesthetic. This includes:
1. **Interactive 3D Flipping Flashcards**: Upgrade the static vocabulary card in the Hero section (and other vocabulary previews) into responsive, double-sided 3D cards that flip upon user interaction (click/tap) and tilt slightly on hover.
2. **Scroll Entrance Animations (AOS & Misty Reveal)**: Set up progressive entrance scroll effects using the built-in AOS library across sections. Add a custom "misty reveal" scroll effect where main headers de-blur (`filter: blur(12px) -> blur(0)`) and fade in.
3. **Drifting Background Fog (Mist Orbs)**: Inject subtle, slow-drifting, blurry color orbs behind the Hero elements using hardware-accelerated CSS keyframe animations to create a premium, modern developer-tool/SaaS atmosphere.

---

## User Review Required

> [!IMPORTANT]
> The animations are strictly client-side CSS and JavaScript, meaning zero impact on backend workflows, database schemas, or page load times.
>
> All custom scroll triggers and background fog effects are designed to be hardware-accelerated (`will-change: transform, filter, opacity`) to prevent page stuttering, particularly on mobile devices.

---

## Proposed Changes

### [Component: Styles]

#### [MODIFY] [home.css](file:///c:/it/ltwnc/wwwroot/css/home.css)
- Define variables for the glassmorphic card look.
- Style the 3D flipping container (`.flashcard-3d-wrapper`, `.flashcard-3d-card`, `.flashcard-front`, `.flashcard-back`).
- Implement smooth hover tilt/lift animations.
- Create drifting mist orb classes (`.fog-container`, `.fog-orb`, `.orb-1`, `.orb-2`, `.orb-3`) and corresponding keyframe animations.
- Implement the `.mist-reveal` class and its active state `.mist-visible`.

---

### [Component: Views]

#### [MODIFY] [Index.cshtml](file:///c:/it/ltwnc/Views/Home/Index.cshtml)
- Wrap the Hero content in a `.hero-container-wrapper` containing `.fog-container` and orbs.
- Replace the static `.home-flashcard-preview` with an interactive 3D glassmorphic card markup.
- Add `data-aos` properties to benefits cards, services grid, checklist items, and quote form to stagger their arrival.
- Add `.mist-reveal` to primary page headers to trigger de-blur on scroll.
- Add a light JavaScript helper at the bottom to handle:
  - Flashcard toggling (`.flipped` state) on click.
  - An `IntersectionObserver` instance that toggles the `.mist-visible` class when headers enter the viewport.

---

## Verification Plan

### Manual Verification
1. **Flashcard 3D Interaction**: Click/tap the Hero vocabulary card. Ensure it flips 180 degrees smoothly showing English on the front, Vietnamese and an example sentence on the back.
2. **Scroll Entrance**: Scroll down the page and verify:
   - Standard sections slide/fade in gracefully via AOS.
   - Primary section headers emerge from a soft blurred (misty) state to full clarity.
3. **Drifting Fog**: Check that the background blobs behind the Hero text slowly drift and change shape without blocking readability of any copy or button.
4. **Performance & Responsiveness**: Run on mobile viewport widths (Chrome DevTools emulation) to verify cards scale appropriately and animations run at 60fps.
