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

// Modal xác nhận thay cho window.confirm để giao diện nhất quán và không hiển
// thị hộp thoại mang tên host của trình duyệt.
(function initializeConfirmDialog() {
    let activeDialog;

    window.appConfirm = function (message) {
        if (activeDialog) return Promise.resolve(false);

        return new Promise(function (resolve) {
            const previousFocus = document.activeElement;
            const backdrop = document.createElement('div');
            backdrop.className = 'app-confirm-backdrop';
            backdrop.innerHTML =
                '<section class="app-confirm" role="dialog" aria-modal="true" aria-labelledby="app-confirm-title" aria-describedby="app-confirm-message">' +
                '<div class="app-confirm__mark" aria-hidden="true">!</div>' +
                '<h2 id="app-confirm-title">Xác nhận thao tác</h2>' +
                '<p id="app-confirm-message"></p>' +
                '<div class="app-confirm__actions">' +
                '<button type="button" class="app-confirm__cancel">Hủy</button>' +
                '<button type="button" class="app-confirm__accept">Xác nhận</button>' +
                '</div></section>';
            backdrop.querySelector('#app-confirm-message').textContent = message;
            const cancel = backdrop.querySelector('.app-confirm__cancel');
            const accept = backdrop.querySelector('.app-confirm__accept');

            function finish(confirmed) {
                document.removeEventListener('keydown', onKeyDown);
                backdrop.remove();
                activeDialog = null;
                if (previousFocus instanceof HTMLElement) previousFocus.focus();
                resolve(confirmed);
            }

            function onKeyDown(event) {
                if (event.key === 'Escape') finish(false);
                if (event.key === 'Tab') {
                    const target = event.shiftKey ? cancel : accept;
                    if (document.activeElement === target) {
                        event.preventDefault();
                        (event.shiftKey ? accept : cancel).focus();
                    }
                }
            }

            cancel.addEventListener('click', function () { finish(false); });
            accept.addEventListener('click', function () { finish(true); });
            backdrop.addEventListener('click', function (event) {
                if (event.target === backdrop) finish(false);
            });
            document.addEventListener('keydown', onKeyDown);
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
})();
