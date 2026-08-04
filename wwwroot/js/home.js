(function () {
    'use strict';

    const POSITION_CLASSES = ['pos-center', 'pos-right', 'pos-back', 'pos-left'];
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

    function initCarousel(root) {
        const items = Array.from(root.querySelectorAll('[data-carousel-item]'));
        const previousButton = root.querySelector('[data-carousel-previous]');
        const nextButton = root.querySelector('[data-carousel-next]');
        const status = root.querySelector('[data-carousel-status]');
        const announcement = root.querySelector('[data-carousel-announcement]');
        const stage = root.querySelector('.home-carousel-stage');

        if (!items.length || !previousButton || !nextButton) return;

        let activeIndex = 0;
        let autoplayId = null;
        let flipTimeout = null;
        let tick = 0;
        let rootInView = true;
        let hovered = false;

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

        /* ---------- autoplay: thẻ tự xoay vòng + thỉnh thoảng tự lật ---------- */

        function autoFlip() {
            const card = items[activeIndex].querySelector('[data-carousel-card]');
            if (!card || card.matches(':hover')) return;
            card.classList.add('flipped');
            card.setAttribute('aria-pressed', 'true');
            flipTimeout = setTimeout(() => resetCard(card), 1900);
        }

        function autoTick() {
            tick += 1;
            if (tick % 2 === 0) autoFlip();
            clearTimeout(flipTimeout);
            activeIndex = (activeIndex + 1) % items.length;
            render(false);
        }

        function startAutoplay() {
            if (prefersReducedMotion.matches || autoplayId !== null || !rootInView || hovered) return;
            autoplayId = setInterval(autoTick, 4200);
        }

        function stopAutoplay() {
            clearInterval(autoplayId);
            clearTimeout(flipTimeout);
            autoplayId = null;
        }

        function restartAutoplay() {
            stopAutoplay();
            startAutoplay();
        }

        if ('IntersectionObserver' in window) {
            new IntersectionObserver((entries) => {
                rootInView = entries[0].isIntersecting;
                if (rootInView) startAutoplay();
                else stopAutoplay();
            }, { threshold: 0.2 }).observe(root);
        } else {
            startAutoplay();
        }

        document.addEventListener('visibilitychange', () => {
            if (document.hidden) stopAutoplay();
            else startAutoplay();
        });

        root.addEventListener('mouseenter', () => { hovered = true; stopAutoplay(); });
        root.addEventListener('mouseleave', () => { hovered = false; startAutoplay(); });
        root.addEventListener('focusin', stopAutoplay);
        root.addEventListener('focusout', startAutoplay);

        /* ---------- pointer tilt: thẻ giữa nghiêng theo con trỏ ---------- */

        if (stage && !prefersReducedMotion.matches) {
            stage.addEventListener('mousemove', (event) => {
                const rect = stage.getBoundingClientRect();
                const x = (event.clientX - rect.left) / rect.width - 0.5;
                const y = (event.clientY - rect.top) / rect.height - 0.5;
                const card = items[activeIndex]?.querySelector('[data-carousel-card]');
                if (!card) return;
                card.style.setProperty('--tilt-y', `${(x * 10).toFixed(2)}deg`);
                card.style.setProperty('--tilt-x', `${(-y * 8).toFixed(2)}deg`);
            });

            stage.addEventListener('mouseleave', () => {
                items.forEach((item) => {
                    const card = item.querySelector('[data-carousel-card]');
                    if (!card) return;
                    card.style.setProperty('--tilt-x', '0deg');
                    card.style.setProperty('--tilt-y', '0deg');
                });
            });
        }

        function move(delta, announce = true) {
            activeIndex = (activeIndex + delta + items.length) % items.length;
            render(announce);
            restartAutoplay();
        }

        previousButton.addEventListener('click', () => move(-1));
        nextButton.addEventListener('click', () => move(1));

        items.forEach((item, index) => {
            const card = item.querySelector('[data-carousel-card]');
            if (!card) return;

            card.addEventListener('click', () => {
                if (index !== activeIndex) return;
                clearTimeout(flipTimeout);
                const flipped = card.classList.toggle('flipped');
                card.setAttribute('aria-pressed', String(flipped));
            });
        });

        render(false);
    }

    /* ---------- scroll reveal: fade-up so le, heading trượt trong mask ---------- */

    function initReveal() {
        document.documentElement.classList.add('js');
        if (!('IntersectionObserver' in window) || prefersReducedMotion.matches) {
            document.querySelectorAll('.home-mask').forEach((el) => el.classList.add('is-in'));
            return;
        }

        const groups = [
            '.home-hero-copy > *',
            '.home-editorial-header',
            '.home-feature-grid > *',
            '.home-feature-row',
            '.home-mode-card',
            '.home-credit-trust-bar',
            '.home-credit-option',
            '.home-set-discovery > *',
            '.home-set-row',
            '.home-testimonial',
            '.home-closing-grid > *'
        ];

        const targets = [];
        groups.forEach((selector) => {
            document.querySelectorAll(selector).forEach((el) => {
                if (el.hasAttribute('data-reveal') || el.classList.contains('home-mask')) return;
                el.setAttribute('data-reveal', '');
                targets.push(el);
            });
        });

        targets.forEach((el) => {
            const siblings = Array.from(el.parentElement?.children || [])
                .filter((child) => child.hasAttribute('data-reveal'));
            el.style.setProperty('--reveal-i', String(Math.max(0, siblings.indexOf(el))));
        });

        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-in');
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.1, rootMargin: '0px 0px -8% 0px' });
        targets.forEach((el) => observer.observe(el));

        const maskObserver = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-in');
                maskObserver.unobserve(entry.target);
            });
        }, { threshold: 0.4 });
        document.querySelectorAll('.home-mask').forEach((el) => maskObserver.observe(el));
    }

    /* ---------- count-up cho các con số nổi bật ---------- */

    function initCounters() {
        const nodes = document.querySelectorAll('[data-count]');
        if (!nodes.length) return;

        const format = (value) => value.toLocaleString('vi-VN');
        const finalize = (el) => {
            el.textContent = format(parseInt(el.dataset.count, 10) || 0) + (el.dataset.suffix || '');
        };

        if (prefersReducedMotion.matches || !('IntersectionObserver' in window)) {
            nodes.forEach(finalize);
            return;
        }

        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                observer.unobserve(entry.target);

                const el = entry.target;
                const target = parseInt(el.dataset.count, 10) || 0;
                const suffix = el.dataset.suffix || '';
                const start = performance.now();
                const duration = 1400;

                function frame(now) {
                    const progress = Math.min(1, (now - start) / duration);
                    const eased = 1 - Math.pow(1 - progress, 3);
                    el.textContent = format(Math.round(target * eased)) + suffix;
                    if (progress < 1) requestAnimationFrame(frame);
                }
                requestAnimationFrame(frame);
            });
        }, { threshold: 0.6 });
        nodes.forEach((el) => observer.observe(el));
    }

    /* ---------- parallax nhẹ cho chữ Aa watermark ---------- */

    function initParallax() {
        const nodes = document.querySelectorAll('.home-word-specimen');
        if (!nodes.length || prefersReducedMotion.matches) return;

        let ticking = false;
        function update() {
            ticking = false;
            const viewport = window.innerHeight;
            nodes.forEach((el) => {
                const rect = el.getBoundingClientRect();
                if (rect.bottom < 0 || rect.top > viewport) return;
                const offset = (rect.top + rect.height / 2 - viewport / 2) * -0.08;
                el.style.setProperty('--parallax', `${offset.toFixed(1)}px`);
            });
        }

        window.addEventListener('scroll', () => {
            if (!ticking) {
                ticking = true;
                requestAnimationFrame(update);
            }
        }, { passive: true });
        update();
    }

    function initHome() {
        document.querySelectorAll('[data-home-carousel]').forEach(initCarousel);
        initReveal();
        initCounters();
        initParallax();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initHome, { once: true });
    } else {
        initHome();
    }
})();
