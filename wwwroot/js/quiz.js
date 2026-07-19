(() => {
    'use strict';

    const root = document.querySelector('[data-quiz-root]');
    if (!root) return;

    const buttons = Array.from(root.querySelectorAll('[data-quiz-answer]'));
    const feedback = root.querySelector('[data-quiz-feedback]');
    const nextLink = root.querySelector('[data-quiz-next]');
    const nextLabel = root.querySelector('[data-quiz-next-label]');
    const timer = root.querySelector('[data-quiz-timer]');
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const deadlineUtc = Date.parse(root.dataset.quizDeadlineUtc);
    let timerIntervalId;
    let timeoutRequested = false;

    const setPending = (pending) => {
        root.setAttribute('aria-busy', pending ? 'true' : 'false');
        buttons.forEach((button) => {
            button.disabled = true;
            if (!pending) button.disabled = false;
        });
    };

    const showRetryableError = () => {
        feedback.textContent = 'Không thể gửi câu trả lời. Vui lòng thử lại.';
        feedback.className = 'quiz-feedback is-error';
        setPending(false);
    };

    const requestTimeout = async () => {
        if (timeoutRequested) return;
        timeoutRequested = true;
        if (timerIntervalId) window.clearInterval(timerIntervalId);

        setPending(true);
        feedback.textContent = 'H\u1ebft th\u1eddi gian. \u0110ang chuy\u1ec3n \u0111\u1ebfn k\u1ebft qu\u1ea3\u2026';
        feedback.className = 'quiz-feedback';

        try {
            const response = await fetch(root.dataset.quizTimeoutUrl, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });
            if (!response.ok) throw new Error('Timeout request failed');

            const result = await response.json();
            if (!result.success || !result.nextUrl) throw new Error('Invalid timeout response');
            window.location.assign(result.nextUrl);
        } catch (error) {
            feedback.textContent = 'H\u1ebft th\u1eddi gian. Kh\u00f4ng th\u1ec3 t\u1ea3i k\u1ebft qu\u1ea3, vui l\u00f2ng t\u1ea3i l\u1ea1i trang.';
            feedback.className = 'quiz-feedback is-error';
        }
    };

    const updateTimer = () => {
        const remainingSeconds = Math.max(0, Math.ceil((deadlineUtc - Date.now()) / 1000));
        const minutes = Math.floor(remainingSeconds / 60);
        const seconds = remainingSeconds % 60;
        timer.textContent = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
        timer.classList.toggle('is-warning', remainingSeconds <= 60);

        if (remainingSeconds === 0) requestTimeout();
    };

    if (timer && Number.isFinite(deadlineUtc)) {
        updateTimer();
        timerIntervalId = window.setInterval(updateTimer, 250);
    }

    buttons.forEach((button) => {
        button.addEventListener('click', async () => {
            if (root.getAttribute('aria-busy') === 'true') return;

            setPending(true);
            feedback.textContent = 'Đang kiểm tra câu trả lời…';
            feedback.className = 'quiz-feedback';

            const selectedChoiceIndex = button.dataset.choiceIndex;
            const body = new URLSearchParams({
                questionId: root.dataset.questionId,
                selectedChoiceIndex
            });

            try {
                const response = await fetch(root.dataset.answerUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
                        'RequestVerificationToken': token,
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    credentials: 'same-origin',
                    body: body.toString()
                });

                if (response.status === 409) {
                    const result = await response.json();
                    if (result.expired && result.nextUrl) {
                        window.location.assign(result.nextUrl);
                        return;
                    }

                    feedback.textContent = 'Câu hỏi này đã được chấm. Đang tải câu tiếp theo…';
                    feedback.className = 'quiz-feedback is-error';
                    window.setTimeout(() => window.location.reload(), 900);
                    return;
                }

                if (response.status >= 500) throw new Error('Server error');
                if (!response.ok) throw new Error('Request failed');

                const result = await response.json();
                const correctChoiceIndex = Number(result.correctChoiceIndex);
                const correctButton = buttons[correctChoiceIndex];
                if (!result.success || !Number.isInteger(correctChoiceIndex) || !correctButton || !result.nextUrl) {
                    throw new Error('Invalid response');
                }

                const correctChoiceText = correctButton.textContent.trim();
                correctButton.classList.add('is-correct');
                correctButton.setAttribute('aria-label', `Đáp án đúng: ${correctChoiceText}`);
                if (result.isCorrect === false) button.classList.add('is-wrong');

                feedback.textContent = result.isCorrect
                    ? 'Chính xác!'
                    : `Chưa đúng. Đáp án đúng: ${correctChoiceText}.`;
                feedback.className = result.isCorrect
                    ? 'quiz-feedback is-correct'
                    : 'quiz-feedback is-wrong';

                nextLink.href = result.nextUrl;
                nextLabel.textContent = result.isLastQuestion ? 'Xem kết quả' : 'Câu tiếp theo';
                nextLink.hidden = false;
                root.setAttribute('aria-busy', 'false');
            } catch (error) {
                showRetryableError();
            }
        });
    });
})();
