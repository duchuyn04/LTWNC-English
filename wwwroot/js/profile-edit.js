(function () {
    // Scroll-spy: highlight menu theo section đang hiển thị
    const links = document.querySelectorAll('.settings-nav-link');
    if (links.length) {
        const sections = [...links]
            .map((link) => document.querySelector(link.getAttribute('href')))
            .filter(Boolean);

        const setActive = (id) => {
            links.forEach((link) => {
                const active = link.getAttribute('href') === '#' + id;
                link.classList.toggle('active', active);
                if (active) link.setAttribute('aria-current', 'location');
                else link.removeAttribute('aria-current');
            });
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) setActive(entry.target.id);
            });
        }, { rootMargin: '-20% 0px -70% 0px' });

        sections.forEach((section) => observer.observe(section));
        const initialSection = window.location.hash.slice(1);
        if (initialSection && sections.some((section) => section.id === initialSection)) {
            setActive(initialSection);
        }

        links.forEach((link) => {
            link.addEventListener('click', (event) => {
                const target = document.querySelector(link.getAttribute('href'));
                if (!target) return;
                event.preventDefault();
                target.scrollIntoView({
                    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                    block: 'start'
                });
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

    // Hiển thị trạng thái đang xử lý để tránh gửi trùng khi mạng chậm.
    document.querySelectorAll('[data-loading-form]').forEach((form) => {
        form.addEventListener('submit', (event) => {
            const submitter = event.submitter || form.querySelector('button[type="submit"]');
            if (!submitter || submitter.dataset.loading === 'true') return;

            const markLoading = () => {
                if (event.defaultPrevented && !form.classList.contains('settings-avatar-form')) return;
                submitter.dataset.loading = 'true';
                submitter.dataset.originalText = submitter.textContent;
                submitter.textContent = submitter.dataset.loadingText || 'Đang xử lý...';
                submitter.disabled = true;
                form.setAttribute('aria-busy', 'true');
            };

            if (form.classList.contains('settings-avatar-form')) {
                markLoading();
            } else {
                // jQuery unobtrusive validation may cancel submit on the next handler.
                window.setTimeout(markLoading, 0);
            }
        });
    });

    document.querySelectorAll('[data-password-toggle]').forEach((toggle) => {
        toggle.addEventListener('click', () => {
            const input = document.getElementById(toggle.getAttribute('aria-controls'));
            if (!input) return;
            const showing = input.type === 'text';
            input.type = showing ? 'password' : 'text';
            toggle.textContent = showing ? 'Hiện' : 'Ẩn';
            toggle.setAttribute('aria-label', showing ? 'Hiện mật khẩu' : 'Ẩn mật khẩu');
            toggle.setAttribute('aria-pressed', String(!showing));
        });
    });
})();
