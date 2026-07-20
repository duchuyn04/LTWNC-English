(function () {
    // Scroll-spy: highlight menu theo section đang hiển thị
    const links = document.querySelectorAll('.settings-nav-link');
    if (links.length) {
        const sections = [...links]
            .map((link) => document.querySelector(link.getAttribute('href')))
            .filter(Boolean);

        const setActive = (id) => {
            links.forEach((link) =>
                link.classList.toggle('active', link.getAttribute('href') === '#' + id));
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) setActive(entry.target.id);
            });
        }, { rootMargin: '-20% 0px -70% 0px' });

        sections.forEach((section) => observer.observe(section));

        links.forEach((link) => {
            link.addEventListener('click', (event) => {
                const target = document.querySelector(link.getAttribute('href'));
                if (!target) return;
                event.preventDefault();
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                history.replaceState(null, '', link.getAttribute('href'));
            });
        });
    }

    // Hiển thị tên file ảnh đã chọn
    const avatarInput = document.querySelector('#avatar');
    const fileName = document.querySelector('[data-avatar-filename]');
    avatarInput?.addEventListener('change', () => {
        if (fileName) {
            fileName.textContent = avatarInput.files?.[0]?.name ?? 'Chưa chọn ảnh nào';
        }
    });
})();
