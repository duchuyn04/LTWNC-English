// Flashcard flip helper (legacy usage)
function toggleFlip() {
    document.querySelector('.flashcard')?.classList.toggle('flipped');
}

// Đưa các thông báo tạm thời vào một popup stack dùng chung. Validation và cảnh
// báo nghiệp vụ lâu dài không có data-popup nên vẫn nằm cạnh nội dung liên quan.
(function initializePopups() {
    const popupSelector = '[data-popup]';
    let stack;

    function getStack() {
        if (stack) return stack;

        stack = document.createElement('div');
        stack.className = 'app-popup-stack';
        stack.setAttribute('aria-label', 'Thông báo');
        document.body.appendChild(stack);
        return stack;
    }

    function dismiss(popup) {
        if (!popup.isConnected || popup.classList.contains('is-leaving')) return;

        popup.classList.add('is-leaving');
        window.setTimeout(function () {
            popup.remove();
            if (stack && !stack.children.length) {
                stack.remove();
                stack = null;
            }
        }, 180);
    }

    function enhance(popup) {
        if (!(popup instanceof HTMLElement) || popup.dataset.popupReady === 'true') return;

        popup.dataset.popupReady = 'true';
        const tone = ['success', 'error', 'info'].includes(popup.dataset.popup)
            ? popup.dataset.popup
            : 'info';
        popup.classList.add('app-popup', 'app-popup--' + tone);
        popup.setAttribute('role', tone === 'error' ? 'alert' : 'status');
        popup.setAttribute('aria-live', tone === 'error' ? 'assertive' : 'polite');

        const contentWrapper = document.createElement('div');
        contentWrapper.className = 'app-popup__content';
        while (popup.firstChild) {
            contentWrapper.appendChild(popup.firstChild);
        }

        const iconWrapper = document.createElement('div');
        iconWrapper.className = 'app-popup__icon';
        const icon = document.createElement('i');
        icon.className = tone === 'error'
            ? 'ph ph-warning-circle'
            : tone === 'success'
                ? 'ph ph-check-circle'
                : 'ph ph-info';
        iconWrapper.appendChild(icon);

        popup.appendChild(iconWrapper);
        popup.appendChild(contentWrapper);

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'app-popup__close';
        close.setAttribute('aria-label', 'Đóng thông báo');
        const closeIcon = document.createElement('i');
        closeIcon.className = 'ph ph-x';
        close.appendChild(closeIcon);
        close.addEventListener('click', function () { dismiss(popup); });
        popup.appendChild(close);
        getStack().appendChild(popup);

        window.requestAnimationFrame(function () {
            popup.classList.add('is-visible');
        });

        if (popup.dataset.popupPersist !== 'true') {
            const duration = tone === 'error' ? 9000 : 6000;
            let timer = window.setTimeout(function () { dismiss(popup); }, duration);
            popup.addEventListener('mouseenter', function () { window.clearTimeout(timer); });
            popup.addEventListener('mouseleave', function () {
                timer = window.setTimeout(function () { dismiss(popup); }, 2500);
            });
        }
    }

    // Các module AJAX có thể gọi API này thay vì tự dựng một alert inline mới.
    window.showAppPopup = function (message, tone, options) {
        const popup = document.createElement('div');
        popup.dataset.popup = tone || 'info';
        if (options?.persist) popup.dataset.popupPersist = 'true';
        popup.textContent = message;
        document.body.appendChild(popup);
        enhance(popup);
        return popup;
    };

    document.querySelectorAll(popupSelector).forEach(enhance);

    // Batch editor tạo thông báo sau khi trang đã tải, nên theo dõi node mới để
    // áp dụng cùng popup mà không buộc từng module JavaScript lặp lại giao diện.
    new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (!(node instanceof Element)) return;
                if (node.matches(popupSelector)) enhance(node);
                node.querySelectorAll(popupSelector).forEach(enhance);
            });
        });
    }).observe(document.body, { childList: true, subtree: true });
})();

// Modal xác nhận dùng chung thay cho window.confirm. Có thể đổi tiêu đề, nhãn
// nút và sắc thái nhưng mọi trang vẫn dùng cùng một cấu trúc, focus trap và CSS.
(function initializeConfirmDialog() {
    let activeDialog;

    window.appConfirm = function (message, options) {
        if (activeDialog) return Promise.resolve(false);

        const settings = {
            title: 'Xác nhận thao tác',
            cancelLabel: 'Hủy',
            acceptLabel: 'Xác nhận',
            tone: 'danger',
            ...(options || {})
        };
        const tone = ['danger', 'warning'].includes(settings.tone) ? settings.tone : 'danger';

        return new Promise(function (resolve) {
            let settled = false;
            const previousFocus = document.activeElement;
            const backdrop = document.createElement('div');
            backdrop.className = 'app-confirm-backdrop';
            backdrop.innerHTML =
                '<section class="app-confirm app-confirm--' + tone + '" role="dialog" aria-modal="true" aria-labelledby="app-confirm-title" aria-describedby="app-confirm-message">' +
                '<div class="app-confirm__mark" aria-hidden="true">!</div>' +
                '<h2 id="app-confirm-title"></h2>' +
                '<p id="app-confirm-message"></p>' +
                '<div class="app-confirm__actions">' +
                '<button type="button" class="app-confirm__cancel"></button>' +
                '<button type="button" class="app-confirm__accept"></button>' +
                '</div></section>';

            const dialog = backdrop.querySelector('.app-confirm');
            const cancel = backdrop.querySelector('.app-confirm__cancel');
            const accept = backdrop.querySelector('.app-confirm__accept');
            backdrop.querySelector('#app-confirm-title').textContent = settings.title;
            backdrop.querySelector('#app-confirm-message').textContent = message;
            cancel.textContent = settings.cancelLabel;
            accept.textContent = settings.acceptLabel;

            function finish(confirmed) {
                if (settled) return;
                settled = true;
                document.removeEventListener('keydown', onKeyDown);
                document.documentElement.classList.remove('app-confirm-open');
                backdrop.remove();
                activeDialog = null;
                if (previousFocus instanceof HTMLElement && previousFocus.isConnected) {
                    previousFocus.focus();
                }
                resolve(confirmed);
            }

            function onKeyDown(event) {
                if (event.key === 'Escape') {
                    event.preventDefault();
                    finish(false);
                    return;
                }

                if (event.key !== 'Tab') return;
                const first = cancel;
                const last = accept;
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault();
                    last.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault();
                    first.focus();
                }
            }

            cancel.addEventListener('click', function () { finish(false); });
            accept.addEventListener('click', function () { finish(true); });
            backdrop.addEventListener('click', function (event) {
                if (event.target === backdrop) finish(false);
            });
            dialog.addEventListener('click', function (event) { event.stopPropagation(); });
            document.addEventListener('keydown', onKeyDown);
            document.documentElement.classList.add('app-confirm-open');
            document.body.appendChild(backdrop);
            activeDialog = backdrop;
            cancel.focus();
        });
    };

    document.addEventListener('submit', async function (event) {
        const form = event.target.closest('form[data-confirm]');
        if (!form || form.dataset.confirmApproved === 'true') {
            if (form) delete form.dataset.confirmApproved;
            return;
        }

        event.preventDefault();
        const submitter = event.submitter;
        if (await window.appConfirm(form.dataset.confirm)) {
            form.dataset.confirmApproved = 'true';
            form.requestSubmit(submitter || undefined);
        }
    }, true);

    // Chặn link và form điều hướng trong website bằng popup chung. Trình duyệt
    // vẫn bắt buộc dùng beforeunload gốc cho đóng tab, reload và nhập URL mới.
    window.createAppNavigationGuard = function (hasUnsavedChanges, options) {
        if (typeof hasUnsavedChanges !== 'function') {
            throw new TypeError('hasUnsavedChanges must be a function.');
        }

        const settings = {
            title: 'Rời trang?',
            message: 'Một số thay đổi chưa được lưu. Nếu rời trang lúc này, nội dung đó có thể bị mất.',
            cancelLabel: 'Tiếp tục chỉnh sửa',
            acceptLabel: 'Rời trang',
            ...(options || {})
        };
        let bypass = false;
        let confirmationPromise = null;

        function shouldWarn() {
            return !bypass && Boolean(hasUnsavedChanges());
        }

        function confirmLeave() {
            if (!shouldWarn()) return Promise.resolve(true);
            if (!confirmationPromise) {
                confirmationPromise = window.appConfirm(settings.message, {
                    title: settings.title,
                    cancelLabel: settings.cancelLabel,
                    acceptLabel: settings.acceptLabel,
                    tone: 'warning'
                }).finally(function () {
                    confirmationPromise = null;
                });
            }
            return confirmationPromise;
        }

        async function onLinkClick(event) {
            if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !shouldWarn()) {
                return;
            }

            const link = event.target instanceof Element ? event.target.closest('a[href]') : null;
            if (!link || link.hasAttribute('download') || link.dataset.bypassNavigationGuard === 'true') return;
            if (link.target && link.target.toLowerCase() !== '_self') return;

            const rawHref = link.getAttribute('href') || '';
            if (!rawHref || /^(#|javascript:|mailto:|tel:)/i.test(rawHref)) return;

            let destination;
            try {
                destination = new URL(link.href, window.location.href);
            } catch {
                return;
            }

            const current = new URL(window.location.href);
            const isSameDocumentHash = destination.origin === current.origin
                && destination.pathname === current.pathname
                && destination.search === current.search
                && destination.hash
                && destination.hash !== current.hash;
            if (isSameDocumentHash) return;

            event.preventDefault();
            event.stopImmediatePropagation();
            if (await confirmLeave()) {
                bypass = true;
                window.location.assign(destination.href);
            }
        }

        async function onFormSubmit(event) {
            if (event.defaultPrevented || !shouldWarn()) return;
            const form = event.target instanceof Element ? event.target.closest('form') : null;
            if (!form || form.dataset.bypassNavigationGuard === 'true') return;
            if (form.target && form.target.toLowerCase() !== '_self') return;

            event.preventDefault();
            event.stopImmediatePropagation();
            const submitter = event.submitter;
            if (await confirmLeave()) {
                bypass = true;
                if (form.hasAttribute('data-confirm')) form.dataset.confirmApproved = 'true';
                form.requestSubmit(submitter || undefined);
            }
        }

        function onBeforeUnload(event) {
            if (!shouldWarn()) return;
            event.preventDefault();
            event.returnValue = '';
        }

        document.addEventListener('click', onLinkClick, true);
        document.addEventListener('submit', onFormSubmit, true);
        window.addEventListener('beforeunload', onBeforeUnload);

        return {
            allowNextNavigation: function () { bypass = true; },
            destroy: function () {
                document.removeEventListener('click', onLinkClick, true);
                document.removeEventListener('submit', onFormSubmit, true);
                window.removeEventListener('beforeunload', onBeforeUnload);
            }
        };
    };
})();
