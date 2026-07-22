(function () {
    'use strict';

    const POSITION_CLASSES = ['pos-center', 'pos-right', 'pos-back', 'pos-left'];

    function initCarousel(root) {
        const items = Array.from(root.querySelectorAll('[data-carousel-item]'));
        const previousButton = root.querySelector('[data-carousel-previous]');
        const nextButton = root.querySelector('[data-carousel-next]');
        const status = root.querySelector('[data-carousel-status]');
        const announcement = root.querySelector('[data-carousel-announcement]');

        if (!items.length || !previousButton || !nextButton) return;

        let activeIndex = 0;

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

        function move(delta) {
            activeIndex = (activeIndex + delta + items.length) % items.length;
            render(true);
        }

        previousButton.addEventListener('click', () => move(-1));
        nextButton.addEventListener('click', () => move(1));

        items.forEach((item, index) => {
            const card = item.querySelector('[data-carousel-card]');
            if (!card) return;

            card.addEventListener('click', () => {
                if (index !== activeIndex) return;
                const flipped = card.classList.toggle('flipped');
                card.setAttribute('aria-pressed', String(flipped));
            });
        });

        render(false);
    }

    function initHome() {
        document.querySelectorAll('[data-home-carousel]').forEach(initCarousel);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initHome, { once: true });
    } else {
        initHome();
    }
})();
