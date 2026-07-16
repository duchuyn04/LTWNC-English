(function () {
    'use strict';

    const maxHeight = 220;

    function cardControls(cardId) {
        return document.querySelectorAll('[data-card-id="' + cardId + '"]');
    }

    function setStarState(cardId, isStarred) {
        cardControls(cardId).forEach(function (control) {
            control.classList.toggle('is-starred', isStarred);
            if (control.classList.contains('star-checkbox')) {
                control.checked = isStarred;
                control.setAttribute('aria-checked', String(isStarred));
            }
        });

        const listIcon = document.querySelector(
            '.vocab-list-item[data-card-id="' + cardId + '"] .vocab-star');
        if (listIcon) {
            listIcon.textContent = isStarred ? '★' : '☆';
        }

        document.querySelectorAll('.star-checkbox[data-card-id="' + cardId + '"]').forEach(function (input) {
            const target = input.dataset.starTarget && document.querySelector(input.dataset.starTarget);
            target?.classList.toggle('is-starred', isStarred);
        });
    }

    function showStarError(input, message) {
        const form = input.closest('.vocab-card-form');
        if (!form) return;

        let error = form.querySelector('.star-error');
        if (!error) {
            error = document.createElement('span');
            error.className = 'star-error text-danger';
            error.setAttribute('role', 'alert');
            form.querySelector('.vocab-form-actions')?.appendChild(error);
        }
        error.textContent = message;
    }

    function clearStarError(input) {
        input.closest('.vocab-card-form')?.querySelector('.star-error')?.remove();
    }

    function toggleStar(input) {
        if (!input || input.getAttribute('data-star-pending') === 'true') return;

        const cardId = input.dataset.cardId;
        const previousChecked = !input.checked;
        const token = input.closest('form')?.querySelector('input[name="__RequestVerificationToken"]')
            || document.querySelector('input[name="__RequestVerificationToken"]');
        const relatedInputs = Array.from(document.querySelectorAll(
            '.star-checkbox[data-card-id="' + cardId + '"]'));
        const relatedForms = Array.from(new Set(relatedInputs
            .map(function (control) { return control.closest('.vocab-card-form'); })
            .filter(Boolean)));

        relatedInputs.forEach(function (control) {
            control.setAttribute('data-star-pending', 'true');
        });
        relatedForms.forEach(function (form) {
            form.setAttribute('data-star-pending', 'true');
        });
        clearStarError(input);

        fetch(input.dataset.toggleStarUrl, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token?.value || '',
                'X-CSRF-TOKEN': token?.value || '',
                'Accept': 'application/json'
            }
        })
            .then(function (response) {
                if (!response.ok) throw new Error('Star request failed');
                return response.json();
            })
            .then(function (result) {
                if (!result || result.success !== true) throw new Error('Star request failed');
                setStarState(cardId, Boolean(result.isStarred));
            })
            .catch(function () {
                setStarState(cardId, previousChecked);
                showStarError(input, 'Không thể cập nhật đánh dấu sao. Vui lòng thử lại.');
            })
            .finally(function () {
                relatedInputs.forEach(function (control) {
                    control.removeAttribute('data-star-pending');
                });
                relatedForms.forEach(function (form) {
                    form.removeAttribute('data-star-pending');
                });
            });
    }

    function selectCard(cardId) {
        document.querySelectorAll('.vocab-list-item[data-card-id]').forEach(function (item) {
            item.classList.toggle('is-active', item.dataset.cardId === String(cardId));
        });

        document.querySelectorAll('[data-card-panel]').forEach(function (panel) {
            const active = panel.dataset.cardPanel === String(cardId);
            panel.classList.toggle('is-active', active);
            if (active) {
                panel.querySelectorAll('textarea[data-auto-grow]').forEach(growTextarea);
                syncEditorPanelHeights();
                if (window.innerWidth <= 900) {
                    panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }
        });
    }

    function growTextarea(textarea) {
        textarea.style.height = 'auto';
        textarea.style.height = Math.min(textarea.scrollHeight, maxHeight) + 'px';
    }

    function syncEditorPanelHeights() {
        const editor = document.querySelector('.vocab-editor');
        const detail = document.querySelector('.vocab-detail');
        if (!editor || !detail) return;

        if (window.innerWidth <= 900) {
            editor.style.removeProperty('--vocab-detail-height');
            return;
        }

        const height = Math.ceil(detail.getBoundingClientRect().height);
        if (height > 0) {
            editor.style.setProperty('--vocab-detail-height', height + 'px');
        }
    }

    function bindAutoGrow(textarea) {
        if (!textarea || textarea.dataset.autoGrowBound === 'true') return;
        textarea.dataset.autoGrowBound = 'true';
        textarea.addEventListener('input', function () { growTextarea(textarea); });
        growTextarea(textarea);
    }

    function bindAnchors() {
        document.querySelectorAll('.set-editor-sidebar a[href^="#"]').forEach(function (anchor) {
            anchor.addEventListener('click', function (event) {
                const target = document.querySelector(anchor.getAttribute('href'));
                if (!target) return;
                event.preventDefault();
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
                target.focus({ preventScroll: true });
            });
        });
    }

    function init() {
        document.querySelectorAll('.vocab-list-item[data-card-id]').forEach(function (button) {
            button.addEventListener('click', function () { selectCard(button.dataset.cardId); });
        });
        document.querySelectorAll('.star-checkbox[data-card-id]').forEach(function (input) {
            input.setAttribute('aria-checked', String(input.checked));
            input.addEventListener('change', function () { toggleStar(input); });
        });
        document.querySelectorAll('.vocab-card-form').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (form.getAttribute('data-star-pending') !== 'true') return;
                event.preventDefault();
                const input = form.querySelector('.star-checkbox[data-card-id]');
                if (input) showStarError(input, 'Đang cập nhật đánh dấu sao. Vui lòng đợi.');
            });
        });
        document.querySelectorAll('textarea[data-auto-grow]').forEach(bindAutoGrow);
        bindAnchors();

        const firstCard = document.querySelector('.vocab-list-item[data-card-id]');
        if (firstCard) selectCard(firstCard.dataset.cardId);
        syncEditorPanelHeights();

        const detail = document.querySelector('.vocab-detail');
        if (detail && window.ResizeObserver) {
            new ResizeObserver(syncEditorPanelHeights).observe(detail);
        }
        window.addEventListener('resize', syncEditorPanelHeights);
    }

    window.selectCard = selectCard;
    window.toggleStar = toggleStar;
    window.bindAutoGrow = bindAutoGrow;
    window.bindAnchors = bindAnchors;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
}());
