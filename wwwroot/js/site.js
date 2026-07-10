// Flashcard flip helper (legacy usage)
function toggleFlip() {
    document.querySelector('.flashcard')?.classList.toggle('flipped');
}

// GSAP entrance animations via utility classes
(function () {
    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function makeScrollTriggerConfig(trigger) {
        return {
            trigger: trigger,
            start: 'top 85%',
            toggleActions: 'play none none none'
        };
    }

    function initGsapAnimations() {
        if (typeof gsap === 'undefined' || typeof ScrollTrigger === 'undefined') return;

        gsap.registerPlugin(ScrollTrigger);

        const reduced = prefersReducedMotion();
        const baseDuration = reduced ? 0.25 : 0.6;
        const baseEase = 'power2.out';

        const entrancePresets = [
            { selector: '.gsap-fade-up', from: { opacity: 0, y: reduced ? 0 : 24 } },
            { selector: '.gsap-fade-in', from: { opacity: 0 } },
            { selector: '.gsap-scale-in', from: { opacity: 0, scale: reduced ? 1 : 0.95 } },
            { selector: '.gsap-from-left', from: { opacity: 0, x: reduced ? 0 : -40 } },
            { selector: '.gsap-from-right', from: { opacity: 0, x: reduced ? 0 : 40 } }
        ];

        entrancePresets.forEach(({ selector, from }) => {
            gsap.utils.toArray(selector).forEach(el => {
                gsap.from(el, {
                    ...from,
                    duration: baseDuration,
                    ease: baseEase,
                    scrollTrigger: makeScrollTriggerConfig(el)
                });
            });
        });

        gsap.utils.toArray('.gsap-stagger').forEach(parent => {
            const children = Array.from(parent.children)
                .filter(c => !c.classList.contains('visually-hidden'));
            if (children.length === 0) return;

            gsap.from(children, {
                opacity: 0,
                y: reduced ? 0 : 24,
                duration: baseDuration,
                ease: baseEase,
                stagger: reduced ? 0.04 : 0.08,
                scrollTrigger: makeScrollTriggerConfig(parent)
            });
        });
    }

    document.addEventListener('DOMContentLoaded', initGsapAnimations);
})();
