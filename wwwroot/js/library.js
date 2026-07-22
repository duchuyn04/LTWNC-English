// Progressive enhancement cho /Library — trang hoạt động đầy đủ khi JS tắt.
(() => {
    const search = document.querySelector('#library-search');
    if (!(search instanceof HTMLInputElement)) return;

    document.addEventListener('keydown', event => {
        const target = event.target;
        const isEditing = target instanceof HTMLElement &&
            (target.matches('input, textarea, select') || target.isContentEditable);
        if (event.key === '/' && !isEditing) {
            event.preventDefault();
            search.focus();
        }
    });
})();
