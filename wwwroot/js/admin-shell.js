(function () {
    function initializeAdminShell() {
        const toggle = document.querySelector('[data-admin-menu-toggle]');
        const panel = document.querySelector('[data-admin-menu-panel]');
        const backdrop = document.querySelector('[data-admin-menu-backdrop]');
        if (!toggle || !panel || !backdrop) {
            return;
        }

        const desktopQuery = window.matchMedia('(min-width: 48rem)');

        function isOpen() {
            return panel.classList.contains('is-open');
        }

        function openDrawer() {
            panel.classList.add('is-open');
            document.body.classList.add('admin-menu-open');
            backdrop.hidden = false;
            toggle.setAttribute('aria-expanded', 'true');

            const firstLink = panel.querySelector('.admin-navigation a[href]')
                || panel.querySelector('a[href], button:not([disabled])');
            if (firstLink) {
                firstLink.focus();
            }
        }

        function closeDrawer(restoreFocus) {
            panel.classList.remove('is-open');
            document.body.classList.remove('admin-menu-open');
            backdrop.hidden = true;
            toggle.setAttribute('aria-expanded', 'false');

            if (restoreFocus) {
                toggle.focus();
            }
        }

        toggle.addEventListener('click', function () {
            if (isOpen()) {
                closeDrawer(true);
                return;
            }

            openDrawer();
        });

        backdrop.addEventListener('click', function () {
            closeDrawer(true);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && isOpen()) {
                event.preventDefault();
                closeDrawer(true);
            }
        });

        panel.addEventListener('click', function (event) {
            if (!desktopQuery.matches && event.target.closest('a[href]')) {
                closeDrawer(false);
            }
        });

        desktopQuery.addEventListener('change', function (event) {
            if (event.matches && isOpen()) {
                closeDrawer(false);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeAdminShell, { once: true });
    } else {
        initializeAdminShell();
    }
}());
