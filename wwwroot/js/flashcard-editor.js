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

    function syncBatchToolbar(form) {
        const toolbar = form?.id
            ? document.querySelector('[data-batch-for="' + form.id + '"]')
            : null;
        if (!toolbar) return;

        const hasSelection = form.querySelector('input[name="selectedCardIds"]:checked');
        toolbar.hidden = !hasSelection;
    }

    function showBatchFeedback(form, message, undoLogId, isError) {
        const feedback = document.querySelector('#batch-feedback');
        if (!feedback) return;

        feedback.replaceChildren();
        const alert = document.createElement('div');
        alert.className = 'alert ' + (isError ? 'alert-danger' : 'alert-success');
        alert.setAttribute('role', 'alert');

        const text = document.createElement('span');
        text.textContent = message;
        alert.appendChild(text);

        if (!isError && undoLogId && form.dataset.undoUrlPrefix) {
            alert.classList.add('d-flex', 'justify-content-between', 'align-items-center');
            const undoForm = document.createElement('form');
            undoForm.method = 'post';
            undoForm.action = form.dataset.undoUrlPrefix + undoLogId;
            undoForm.className = 'm-0';

            const token = form.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                const tokenCopy = document.createElement('input');
                tokenCopy.type = 'hidden';
                tokenCopy.name = token.name;
                tokenCopy.value = token.value;
                undoForm.appendChild(tokenCopy);
            }

            const undoButton = document.createElement('button');
            undoButton.type = 'submit';
            undoButton.className = 'btn btn-sm btn-link';
            undoButton.textContent = 'Hoàn tác';
            undoForm.appendChild(undoButton);
            alert.appendChild(undoForm);
        }

        feedback.appendChild(alert);
    }

    function removeCards(cardIds) {
        cardIds.forEach(function (cardId) {
            const checkbox = document.querySelector(
                '#batch-form input[name="selectedCardIds"][value="' + cardId + '"]');
            const wrapper = checkbox?.closest('.vocab-list-item-wrapper');
            const panel = document.querySelector('[data-card-panel="' + cardId + '"]');
            wrapper?.remove();
            panel?.remove();
        });

        const firstCard = document.querySelector('.vocab-list-item[data-card-id]');
        const activeCard = document.querySelector('.vocab-list-item.is-active[data-card-id]');
        const emptyState = document.querySelector('[data-empty-card-list]');
        const detailEmptyState = document.querySelector('[data-empty-detail]');
        const editor = document.querySelector('.vocab-editor');
        const hasCards = Boolean(firstCard);
        if (emptyState) emptyState.hidden = hasCards;
        if (detailEmptyState) detailEmptyState.hidden = hasCards;
        if (editor) editor.classList.toggle('is-empty', !hasCards);
        if (!activeCard && firstCard) selectCard(firstCard.dataset.cardId);
        syncEditorPanelHeights();
    }

    function applyBatchResult(form, result) {
        const cardIds = Array.isArray(result.cardIds) ? result.cardIds : [];
        if (result.action === 'Delete') {
            removeCards(cardIds);
        } else if (result.action === 'Star' || result.action === 'Unstar') {
            cardIds.forEach(function (cardId) {
                setStarState(cardId, result.action === 'Star');
            });
        }

        form.querySelectorAll('input[name="selectedCardIds"]').forEach(function (input) {
            input.checked = false;
        });
        syncBatchToolbar(form);
        showBatchFeedback(form, result.message, result.undoLogId, false);
    }

    function submitBatchAction(form, submitter) {
        if (!submitter || form.dataset.batchPending === 'true') return;

        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        const formData = new FormData(form);
        formData.append('action', submitter.value);
        const toolbarButtons = document.querySelectorAll(
            '[data-batch-for="' + form.id + '"] button');
        form.dataset.batchPending = 'true';
        toolbarButtons.forEach(function (button) { button.disabled = true; });

        fetch(form.getAttribute('action'), {
            method: 'POST',
            credentials: 'same-origin',
            body: formData,
            headers: {
                'RequestVerificationToken': token?.value || '',
                'X-CSRF-TOKEN': token?.value || '',
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'application/json'
            }
        })
            .then(function (response) {
                return response.text().then(function (responseText) {
                    let result = null;
                    try {
                        if (responseText.trim()) result = JSON.parse(responseText);
                    } catch (error) {
                        result = null;
                    }

                    if (!response.ok || !result || result.success !== true) {
                        throw new Error(
                            result?.message ||
                            'Không thể thực hiện thao tác. Vui lòng thử lại.');
                    }

                    return result;
                });
            })
            .then(function (result) { applyBatchResult(form, result); })
            .catch(function (error) {
                showBatchFeedback(
                    form,
                    error.message || 'Không thể thực hiện thao tác. Vui lòng thử lại.',
                    null,
                    true);
            })
            .finally(function () {
                form.removeAttribute('data-batch-pending');
                toolbarButtons.forEach(function (button) { button.disabled = false; });
            });
    }

    function submitBatchActionFromButton(button) {
        const formId = button?.getAttribute('form');
        const form = formId ? document.getElementById(formId) : button?.form;
        if (!form) return true;

        submitBatchAction(form, button);
        return false;
    }

    function bindBatchSelection() {
        document.querySelectorAll('form#batch-form').forEach(function (form) {
            syncBatchToolbar(form);
            form.querySelectorAll('input[name="selectedCardIds"]').forEach(function (input) {
                input.addEventListener('change', function () { syncBatchToolbar(form); });
            });
            const toolbar = document.querySelector('[data-batch-for="' + form.id + '"]');
            toolbar?.querySelectorAll('button').forEach(function (button) {
                button.addEventListener('click', function (event) {
                    if (event.defaultPrevented) return;
                    event.preventDefault();
                    submitBatchAction(form, button);
                });
            });
            form.addEventListener('submit', function (event) {
                event.preventDefault();
                submitBatchAction(form, event.submitter);
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
        bindBatchSelection();

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
    window.submitBatchActionFromButton = submitBatchActionFromButton;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
}());
