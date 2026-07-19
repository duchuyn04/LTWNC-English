// PROTOTYPE ONLY — URL-stable A/B/C switcher for dashboard layout review.
(function () {
    const switcher = document.querySelector('.prototype-switcher');
    const shell = document.querySelector('.admin-shell');
    if (!switcher || !shell) return;

    const variants = ['A', 'B', 'C'];
    const names = {
        A: 'Command Center',
        B: 'Editorial Briefing',
        C: 'Operations Desk'
    };
    const current = (switcher.dataset.currentVariant || 'A').toUpperCase();
    const label = switcher.querySelector('strong em');
    if (label) label.textContent = names[current];

    function navigate(direction) {
        const currentIndex = Math.max(0, variants.indexOf(current));
        const offset = direction === 'previous' ? -1 : 1;
        const nextIndex = (currentIndex + offset + variants.length) % variants.length;
        const url = new URL(window.location.href);
        url.searchParams.set('variant', variants[nextIndex]);
        window.location.assign(url.toString());
    }

    switcher.querySelectorAll('[data-direction]').forEach(function (button) {
        button.addEventListener('click', function () {
            navigate(button.dataset.direction);
        });
    });

    document.addEventListener('keydown', function (event) {
        const target = event.target;
        const isEditing = target instanceof HTMLElement
            && (target.matches('input, textarea, select') || target.isContentEditable);
        if (isEditing) return;
        if (event.key === 'ArrowLeft') navigate('previous');
        if (event.key === 'ArrowRight') navigate('next');
    });
})();
