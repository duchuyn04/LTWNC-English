(function () {
    'use strict';

    const POSITION_CLASSES = ['pos-center', 'pos-right', 'pos-back', 'pos-left'];

    function initCarousel(root) {
        const items = Array.from(root.querySelectorAll('[data-carousel-item]'));
        const previousButton = root.querySelector('[data-carousel-previous]');
        const nextButton = root.querySelector('[data-carousel-next]');
        const toggleButton = root.querySelector('[data-carousel-toggle]');
        const toggleIcon = root.querySelector('[data-carousel-toggle-icon]');
        const toggleLabel = root.querySelector('[data-carousel-toggle-label]');
        const status = root.querySelector('[data-carousel-status]');
        const announcement = root.querySelector('[data-carousel-announcement]');
        const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
        const autoplayMs = Number.parseInt(root.dataset.autoplayMs || '4500', 10);

        if (!items.length || !previousButton || !nextButton || !toggleButton) return;

        let activeIndex = 0;
        let autoplayTimer = null;
        let userPaused = false;
        let pointerInside = false;
        let focusInside = false;

        function resetCard(card) {
            card.classList.remove('flipped');
            card.setAttribute('aria-pressed', 'false');
        }

        function render(announce) {
            items.forEach((item, index) => {
                const relativePosition = (index - activeIndex + items.length) % items.length;
                const card = item.querySelector('[data-carousel-card]');
                const isActive = relativePosition === 0;

                item.classList.remove(...POSITION_CLASSES);
                item.classList.add(POSITION_CLASSES[relativePosition]);
                item.setAttribute('aria-hidden', String(!isActive));

                if (card) {
                    card.tabIndex = isActive ? 0 : -1;
                    if (!isActive) resetCard(card);
                }
            });

            const positionText = `${activeIndex + 1} / ${items.length}`;
            if (status) status.textContent = positionText;
            if (announce && announcement) {
                const activeWord = items[activeIndex].querySelector('.fc-word')?.textContent?.trim();
                announcement.textContent = activeWord
                    ? `Thẻ ${activeIndex + 1} trên ${items.length}: ${activeWord}`
                    : `Thẻ ${activeIndex + 1} trên ${items.length}`;
            }
        }

        function clearAutoplay() {
            if (autoplayTimer === null) return;
            window.clearInterval(autoplayTimer);
            autoplayTimer = null;
        }

        function canAutoplay() {
            return !reducedMotion.matches
                && !userPaused
                && !pointerInside
                && !focusInside
                && !document.hidden;
        }

        function syncAutoplay() {
            clearAutoplay();
            if (!canAutoplay()) return;
            autoplayTimer = window.setInterval(() => move(1, false), autoplayMs);
        }

        function updateToggle() {
            if (reducedMotion.matches) {
                toggleButton.hidden = true;
                toggleButton.disabled = true;
                toggleButton.setAttribute('aria-pressed', 'true');
                toggleButton.setAttribute('aria-label', 'Chuyển thẻ tự động đã tắt');
                toggleIcon.className = 'ph ph-pause';
                toggleLabel.textContent = 'Đã tắt';
                return;
            }

            toggleButton.hidden = false;
            toggleButton.disabled = false;
            toggleButton.setAttribute('aria-pressed', String(userPaused));
            toggleButton.setAttribute('aria-label', userPaused ? 'Tiếp tục chuyển thẻ' : 'Tạm dừng chuyển thẻ');
            toggleIcon.className = userPaused ? 'ph ph-play' : 'ph ph-pause';
            toggleLabel.textContent = userPaused ? 'Tiếp tục' : 'Tạm dừng';
        }

        function move(delta, fromUser) {
            activeIndex = (activeIndex + delta + items.length) % items.length;
            if (fromUser) userPaused = true;
            render(fromUser);
            updateToggle();
            syncAutoplay();
        }

        previousButton.addEventListener('click', () => move(-1, true));
        nextButton.addEventListener('click', () => move(1, true));

        toggleButton.addEventListener('click', () => {
            if (reducedMotion.matches) return;
            userPaused = !userPaused;
            updateToggle();
            syncAutoplay();
        });

        items.forEach((item, index) => {
            const card = item.querySelector('[data-carousel-card]');
            if (!card) return;

            card.addEventListener('click', () => {
                if (index !== activeIndex) return;
                const flipped = card.classList.toggle('flipped');
                card.setAttribute('aria-pressed', String(flipped));
            });
        });

        root.addEventListener('mouseenter', () => {
            pointerInside = true;
            syncAutoplay();
        });

        root.addEventListener('mouseleave', () => {
            pointerInside = false;
            syncAutoplay();
        });

        root.addEventListener('focusin', () => {
            focusInside = true;
            syncAutoplay();
        });

        root.addEventListener('focusout', () => {
            window.requestAnimationFrame(() => {
                focusInside = root.contains(document.activeElement);
                syncAutoplay();
            });
        });

        document.addEventListener('visibilitychange', syncAutoplay);

        const handleMotionPreference = () => {
            updateToggle();
            syncAutoplay();
        };

        if (typeof reducedMotion.addEventListener === 'function') {
            reducedMotion.addEventListener('change', handleMotionPreference);
        } else if (typeof reducedMotion.addListener === 'function') {
            reducedMotion.addListener(handleMotionPreference);
        }

        render(false);
        updateToggle();
        syncAutoplay();
    }

    function initAnchorLinks() {
        document.querySelectorAll('.home-landing a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', event => {
                const target = document.querySelector(anchor.getAttribute('href'));
                if (!target) return;
                event.preventDefault();
                target.scrollIntoView({
                    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                    block: 'start'
                });
            });
        });
    }

    function initScrollReveal() {
        const landing = document.querySelector('.home-landing');
        if (!landing) return;

        const targets = Array.from(landing.querySelectorAll('[data-home-reveal]'));
        const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (!targets.length || reducedMotion || !('IntersectionObserver' in window)) return;

        landing.classList.add('home-motion-ready');

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            });
        }, {
            threshold: 0.12,
            rootMargin: '0px 0px -8% 0px'
        });

        targets.forEach(target => observer.observe(target));
    }

    function initHome() {
        initScrollReveal();
        document.querySelectorAll('[data-home-carousel]').forEach(initCarousel);
        initAnchorLinks();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initHome, { once: true });
    } else {
        initHome();
    }
})();
