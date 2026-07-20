// PROTOTYPE ONLY: chuyển mẫu bằng URL, không gọi API và không lưu dữ liệu.
(() => {
    const root = document.querySelector('[data-library-prototype]');
    if (!root) return;

    const variants = [
        { key: 'A', label: 'Catalog tìm kiếm trước' },
        { key: 'B', label: 'Editorial tuyển chọn' },
        { key: 'C', label: 'Explorer có bộ lọc' }
    ];
    const current = Math.max(0, variants.findIndex(item => item.key === root.dataset.variant));
    const label = root.querySelector('[data-variant-label]');
    if (label) label.textContent = variants[current].label;

    const go = offset => {
        const next = variants[(current + offset + variants.length) % variants.length];
        const url = new URL(window.location.href);
        url.searchParams.set('variant', next.key);
        window.location.assign(url);
    };

    root.querySelector('[data-variant-previous]')?.addEventListener('click', () => go(-1));
    root.querySelector('[data-variant-next]')?.addEventListener('click', () => go(1));

    document.addEventListener('keydown', event => {
        const target = event.target;
        if (target instanceof HTMLElement && (target.matches('input, textarea, select') || target.isContentEditable)) return;
        if (event.key === 'ArrowLeft') go(-1);
        if (event.key === 'ArrowRight') go(1);
    });

    root.querySelectorAll('[data-favorite]').forEach(button => {
        button.addEventListener('click', () => {
            const saved = button.getAttribute('aria-pressed') !== 'true';
            button.setAttribute('aria-pressed', String(saved));
            button.classList.toggle('is-saved', saved);
            const icon = button.querySelector('i');
            icon?.classList.toggle('ph-fill', saved);
        });
    });
})();
