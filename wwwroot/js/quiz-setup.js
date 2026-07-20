(() => {
    'use strict';

    const root = document.querySelector('[data-quiz-setup]');
    if (!root) return;

    const options = Array.from(root.querySelectorAll('[data-quiz-option]'));
    const modeInput = root.querySelector('[data-quiz-mode-input]');
    const presetInput = root.querySelector('[data-quiz-preset-input]');
    const customInput = root.querySelector('[data-quiz-custom-input]');
    const submitLabel = root.querySelector('[data-quiz-submit-label]');
    const form = root.querySelector('form');

    const applyOption = (option) => {
        const mode = option?.dataset.quizMode ?? 'Preset';
        options.forEach((item) => {
            item.checked = item === option;
            item.closest('.quiz-timing-card')?.classList.toggle(
                'is-selected',
                item === option);
        });

        if (modeInput) modeInput.value = mode;
        if (mode === 'Preset' && presetInput) {
            presetInput.value = option?.dataset.quizMinutes ?? '10';
        }
        if (presetInput) presetInput.disabled = mode !== 'Preset';
        if (customInput) customInput.disabled = mode !== 'Custom';
        if (submitLabel) {
            submitLabel.textContent = mode === 'Untimed'
                ? 'Bắt đầu không giới hạn'
                : 'Bắt đầu bài kiểm tra';
        }
    };

    const initialMode = modeInput?.value || 'Preset';
    const initialPreset = presetInput?.value || '10';
    const initialOption = options.find((option) =>
        option.dataset.quizMode === initialMode
        && (initialMode !== 'Preset'
            || option.dataset.quizMinutes === initialPreset))
        ?? options.find((option) => option.dataset.quizMinutes === '10')
        ?? options[0];

    options.forEach((option) => option.addEventListener('change', () => {
        if (option.checked) applyOption(option);
    }));

    root.querySelector('[data-quiz-custom]')?.addEventListener('click', () => {
        const option = options.find((item) => item.dataset.quizMode === 'Custom');
        if (option) applyOption(option);
    });

    form?.addEventListener('submit', () => {
        if (!form.checkValidity() || !submitLabel) return;
        submitLabel.disabled = true;
        submitLabel.setAttribute('aria-busy', 'true');
    });

    applyOption(initialOption);
})();
