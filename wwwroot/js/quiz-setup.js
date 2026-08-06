(() => {
    'use strict';

    const root = document.querySelector('[data-quiz-setup]');
    if (!root) return;

    const form = root.querySelector('form.qz-setup-panel') || root.querySelector('form');
    const options = Array.from(root.querySelectorAll('[data-quiz-option]'));
    const modeInput = root.querySelector('[data-quiz-mode-input]');
    const presetInput = root.querySelector('[data-quiz-preset-input]');
    const customInput = root.querySelector('[data-quiz-custom-input]');
    const questionCountInput = root.querySelector('[data-quiz-question-count]');
    const questionCountMessage = root.querySelector('[data-quiz-count-validation]');
    const submitLabel = root.querySelector('[data-quiz-submit-label]');
    const summaryText = root.querySelector('[data-quiz-summary-text]');
    const countChips = Array.from(root.querySelectorAll('[data-quiz-count-chip]'));
    const maximumQuestionCount = Number(
        questionCountInput?.max || root.dataset.availableCount || 0);

    const setSelectedCard = (option) => {
        options.forEach((item) => {
            item.checked = item === option;
            const card = item.closest('.qz-time');
            card?.classList.toggle('is-selected', item === option);
        });
    };

    const formatSummary = () => {
        if (!summaryText) return;

        const rawCount = questionCountInput?.value?.trim() ?? '';
        const countLabel = rawCount === ''
            ? `Tất cả ${maximumQuestionCount} câu`
            : `${rawCount} câu`;

        const mode = modeInput?.value || 'Preset';
        let timeLabel = '10 phút';
        if (mode === 'Untimed') {
            timeLabel = 'không giới hạn';
        } else if (mode === 'Custom') {
            const mins = customInput?.value?.trim();
            timeLabel = mins ? `${mins} phút` : 'tùy chỉnh';
        } else if (mode === 'Preset') {
            timeLabel = `${presetInput?.value || '10'} phút`;
        }

        summaryText.textContent = `${countLabel} · ${timeLabel}`;
    };

    const syncCountChips = () => {
        const value = questionCountInput?.value?.trim() ?? '';
        countChips.forEach((chip) => {
            const chipValue = String(chip.dataset.quizCountChip ?? '');
            const isAllChip = Number(chipValue) === maximumQuestionCount;
            const selected = value === ''
                ? isAllChip
                : chipValue === value;
            chip.classList.toggle('is-selected', selected);
        });
    };

    const validateQuestionCount = () => {
        if (!questionCountInput) return true;

        const value = questionCountInput.value.trim();
        let message = '';
        if (questionCountInput.validity.badInput) {
            message = 'Nhập số câu hợp lệ.';
        } else if (value !== '') {
            const count = Number(value);
            if (!Number.isInteger(count) || count < 1) {
                message = 'Số câu phải là số nguyên lớn hơn 0.';
            } else if (count > maximumQuestionCount) {
                message = `Chỉ có ${maximumQuestionCount} câu. Chọn tối đa ${maximumQuestionCount}.`;
            }
        }

        questionCountInput.setCustomValidity(message);
        questionCountInput.setAttribute('aria-invalid', message ? 'true' : 'false');
        if (questionCountMessage) questionCountMessage.textContent = message;
        syncCountChips();
        formatSummary();
        return message === '';
    };

    const applyOption = (option) => {
        const mode = option?.dataset.quizMode ?? 'Preset';
        setSelectedCard(option);

        if (modeInput) modeInput.value = mode;
        if (mode === 'Preset' && presetInput) {
            presetInput.value = option?.dataset.quizMinutes ?? '10';
        }
        if (presetInput) presetInput.disabled = mode !== 'Preset';
        if (customInput) {
            customInput.disabled = mode !== 'Custom';
            if (mode === 'Custom' && !customInput.value) customInput.value = '25';
        }

        formatSummary();
    };

    countChips.forEach((chip) => {
        chip.addEventListener('click', () => {
            const n = Number(chip.dataset.quizCountChip);
            if (!questionCountInput || !Number.isFinite(n)) return;
            questionCountInput.value = n === maximumQuestionCount ? '' : String(n);
            validateQuestionCount();
        });
    });

    const initialMode = modeInput?.value || 'Preset';
    const initialPreset = presetInput?.value || '10';
    const initialOption = options.find((option) =>
        option.dataset.quizMode === initialMode
        && (initialMode !== 'Preset' || option.dataset.quizMinutes === initialPreset))
        ?? options.find((option) => option.dataset.quizMinutes === '10')
        ?? options[0];

    options.forEach((option) => {
        option.addEventListener('change', () => {
            if (option.checked) applyOption(option);
        });
    });

    root.querySelector('[data-quiz-custom]')?.addEventListener('click', (event) => {
        if (event.target === customInput) return;
        const option = options.find((item) => item.dataset.quizMode === 'Custom');
        if (option) applyOption(option);
    });

    customInput?.addEventListener('input', formatSummary);
    customInput?.addEventListener('focus', () => {
        const option = options.find((item) => item.dataset.quizMode === 'Custom');
        if (option && !option.checked) applyOption(option);
    });

    questionCountInput?.addEventListener('input', validateQuestionCount);
    questionCountInput?.addEventListener('change', validateQuestionCount);

    form?.addEventListener('submit', (event) => {
        if (!validateQuestionCount() || !form.checkValidity()) {
            event.preventDefault();
            return;
        }
        if (!submitLabel) return;
        submitLabel.disabled = true;
        submitLabel.setAttribute('aria-busy', 'true');
        const icon = submitLabel.querySelector('i');
        submitLabel.childNodes.forEach((node) => {
            if (node.nodeType === Node.TEXT_NODE) node.remove();
        });
        submitLabel.insertBefore(document.createTextNode('Đang tạo bài… '), icon || null);
    });

    applyOption(initialOption);
    validateQuestionCount();
})();
