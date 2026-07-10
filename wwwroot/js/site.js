// Flashcard flip helper (legacy usage)
function toggleFlip() {
    document.querySelector('.flashcard')?.classList.toggle('flipped');
}

// GSAP entrance animations via utility classes
(function () {
    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function animateIfEnabled(target, vars) {
        if (typeof gsap === 'undefined' || typeof ScrollTrigger === 'undefined') return;
        gsap.from(target, vars);
    }

    function initGsapAnimations() {
        if (typeof gsap === 'undefined' || typeof ScrollTrigger === 'undefined') return;

        gsap.registerPlugin(ScrollTrigger);

        const reduced = prefersReducedMotion();
        const baseDuration = reduced ? 0.25 : 0.6;
        const baseEase = 'power2.out';
        const baseStart = 'top 85%';
        const baseToggle = 'play none none none';

        const scrollTrigger = (trigger) => ({
            trigger: trigger,
            start: baseStart,
            toggleActions: baseToggle
        });

        // Individual entrances
        gsap.utils.toArray('.gsap-fade-up').forEach(el => {
            animateIfEnabled(el, {
                opacity: 0,
                y: reduced ? 0 : 24,
                duration: baseDuration,
                ease: baseEase,
                scrollTrigger: scrollTrigger(el)
            });
        });

        gsap.utils.toArray('.gsap-fade-in').forEach(el => {
            animateIfEnabled(el, {
                opacity: 0,
                duration: baseDuration,
                ease: baseEase,
                scrollTrigger: scrollTrigger(el)
            });
        });

        gsap.utils.toArray('.gsap-scale-in').forEach(el => {
            animateIfEnabled(el, {
                opacity: 0,
                scale: reduced ? 1 : 0.95,
                duration: baseDuration,
                ease: baseEase,
                scrollTrigger: scrollTrigger(el)
            });
        });

        gsap.utils.toArray('.gsap-from-left').forEach(el => {
            animateIfEnabled(el, {
                opacity: 0,
                x: reduced ? 0 : -40,
                duration: baseDuration,
                ease: baseEase,
                scrollTrigger: scrollTrigger(el)
            });
        });

        gsap.utils.toArray('.gsap-from-right').forEach(el => {
            animateIfEnabled(el, {
                opacity: 0,
                x: reduced ? 0 : 40,
                duration: baseDuration,
                ease: baseEase,
                scrollTrigger: scrollTrigger(el)
            });
        });

        // Staggered list entrances
        gsap.utils.toArray('.gsap-stagger').forEach(parent => {
            const children = Array.from(parent.children).filter(c => !c.classList.contains('visually-hidden'));
            if (children.length === 0) return;

            animateIfEnabled(children, {
                opacity: 0,
                y: reduced ? 0 : 24,
                duration: baseDuration,
                ease: baseEase,
                stagger: reduced ? 0.04 : 0.08,
                scrollTrigger: scrollTrigger(parent)
            });
        });
    }

    document.addEventListener('DOMContentLoaded', initGsapAnimations);
})();
