(function () {
    function initializeAdminShell() {
        const toggle = document.querySelector('[data-admin-menu-toggle]');
        const panel = document.querySelector('[data-admin-menu-panel]');
        const backdrop = document.querySelector('[data-admin-menu-backdrop]');
        const workspace = document.querySelector('.admin-workspace');
        if (!toggle || !panel || !backdrop) {
            return;
        }

        const desktopQuery = window.matchMedia('(min-width: 48rem)');

        function isOpen() {
            return panel.classList.contains('is-open');
        }

        function setWorkspaceInert(value) {
            if (!workspace) {
                return;
            }

            if ('inert' in workspace) {
                workspace.inert = value;
            } else if (value) {
                workspace.setAttribute('aria-hidden', 'true');
            } else {
                workspace.removeAttribute('aria-hidden');
            }
        }

        function getFocusableElements() {
            return Array.from(panel.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), ' +
                'select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
            )).filter(function (element) {
                return element.offsetParent !== null;
            });
        }

        function openDrawer() {
            panel.classList.add('is-open');
            document.body.classList.add('admin-menu-open');
            backdrop.hidden = false;
            toggle.setAttribute('aria-expanded', 'true');
            setWorkspaceInert(true);

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
            setWorkspaceInert(false);

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
            if (!isOpen() || desktopQuery.matches) {
                return;
            }

            if (event.key === 'Escape') {
                event.preventDefault();
                closeDrawer(true);
                return;
            }

            if (event.key !== 'Tab') {
                return;
            }

            const focusableElements = getFocusableElements();
            if (focusableElements.length === 0) {
                event.preventDefault();
                return;
            }

            const firstFocusable = focusableElements[0];
            const lastFocusable = focusableElements[focusableElements.length - 1];
            if (event.shiftKey && document.activeElement === firstFocusable) {
                event.preventDefault();
                lastFocusable.focus();
            } else if (!event.shiftKey && document.activeElement === lastFocusable) {
                event.preventDefault();
                firstFocusable.focus();
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
