(() => {
    'use strict';

    const root = document.querySelector('[data-quiz-root]');
    if (!root) return;

    const buttons = Array.from(root.querySelectorAll('[data-quiz-answer]'));
    const feedback = root.querySelector('[data-quiz-feedback]');
    const nextLink = root.querySelector('[data-quiz-next]');
    const nextLabel = root.querySelector('[data-quiz-next-label]');
    const timer = root.querySelector('[data-quiz-timer]');
    const timerAnnouncement = root.querySelector('[data-quiz-timer-announcement]');
    const progress = root.querySelector('[data-quiz-progress]');
    const progressBar = root.querySelector('[data-quiz-progress-bar]');
    const progressCount = root.querySelector('[data-quiz-progress-count]');
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    let calibratedDeadlineUtc = Date.parse(root.dataset.quizDeadlineUtc);
    const rawRemainingSeconds = root.dataset.quizRemainingSeconds;
    const serverRemainingSeconds = rawRemainingSeconds === ''
        ? Number.NaN
        : Number(rawRemainingSeconds);
    if (Number.isFinite(serverRemainingSeconds)) {
        calibratedDeadlineUtc = Date.now() + serverRemainingSeconds * 1000;
    }
    let deadlineUtc = calibratedDeadlineUtc;
    let timerIntervalId;
    let timeoutRequested = false;
    let lastTimerSeconds = null;
    const announcedTimerThresholds = new Set();
    const reviewOnly = root.dataset.quizReviewOnly === 'true';

    const setPending = (pending) => {
        root.setAttribute('aria-busy', pending ? 'true' : 'false');
        buttons.forEach((button) => {
            button.disabled = pending || reviewOnly;
            if (pending) button.disabled = true;
            if (!pending && !reviewOnly) button.disabled = false;
        });
    };

    const updateAnsweredProgress = () => {
        if (!progress || !progressBar) return;

        const current = Number(progress.getAttribute('aria-valuenow'));
        const total = Number(progress.getAttribute('aria-valuemax'));
        if (!Number.isFinite(current) || !Number.isFinite(total)) return;

        const answered = Math.min(total, current + 1);
        progress.setAttribute('aria-valuenow', String(answered));
        progressBar.style.width = `${total > 0 ? answered * 100 / total : 0}%`;
        if (progressCount) {
            progressCount.textContent = `Đã trả lời ${answered} / ${total}`;
        }
    };

    const startTimer = () => {
        if (timerIntervalId) window.clearInterval(timerIntervalId);
        if (timer && Number.isFinite(deadlineUtc)) {
            updateTimer();
            timerIntervalId = window.setInterval(updateTimer, 1000);
        }
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
            if (!response.ok && response.status === 409) {
                const result = await response.json();
                if (result.stale && result.nextUrl) {
                    window.location.assign(result.nextUrl);
                    return;
                }
                if (result.expired && result.nextUrl) {
                    window.location.assign(result.nextUrl);
                    return;
                }
                if (result.expired === false && Number.isFinite(Number(result.remainingSeconds))) {
                    resumeFromServer(Number(result.remainingSeconds));
                    feedback.textContent = 'Máy chủ vẫn còn thời gian. Bạn có thể tiếp tục.';
                    feedback.className = 'quiz-feedback is-error';
                    return;
                }
            }
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

        if (timerAnnouncement) {
            const crossedThreshold = [10, 30, 60].find((threshold) =>
                !announcedTimerThresholds.has(threshold)
                && (lastTimerSeconds === null
                    ? remainingSeconds === threshold
                    : lastTimerSeconds > threshold && remainingSeconds <= threshold));
            if (crossedThreshold !== undefined) {
                announcedTimerThresholds.add(crossedThreshold);
                timerAnnouncement.textContent = crossedThreshold === 60
                    ? 'Còn 1 phút.'
                    : `Còn ${crossedThreshold} giây.`;
            }
        }
        lastTimerSeconds = remainingSeconds;

        if (remainingSeconds === 0) requestTimeout();
    };

    const resumeFromServer = (remainingSeconds) => {
        calibratedDeadlineUtc = Date.now() + remainingSeconds * 1000;
        deadlineUtc = calibratedDeadlineUtc;
        timeoutRequested = false;
        lastTimerSeconds = null;
        announcedTimerThresholds.clear();
        if (timerAnnouncement) timerAnnouncement.textContent = '';
        setPending(false);
        startTimer();
    };

    startTimer();

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
                    if (result.stale && result.nextUrl) {
                        window.location.assign(result.nextUrl);
                        return;
                    }
                    if (result.expired === false && Number.isFinite(Number(result.remainingSeconds))) {
                        resumeFromServer(Number(result.remainingSeconds));
                        feedback.textContent = 'Máy chủ vẫn còn thời gian. Bạn có thể tiếp tục.';
                        feedback.className = 'quiz-feedback is-error';
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

                const correctChoiceText =
                    correctButton.querySelector('[data-quiz-choice-text]')?.textContent.trim() || '';
                const selectedChoiceText =
                    button.querySelector('[data-quiz-choice-text]')?.textContent.trim() || '';
                correctButton.classList.add('is-correct');
                correctButton.setAttribute('aria-label', `Đáp án đúng: ${correctChoiceText}`);
                if (result.isCorrect === false) {
                    button.classList.add('is-wrong');
                    button.setAttribute(
                        'aria-label',
                        `Bạn đã chọn: ${selectedChoiceText}. Chưa đúng.`);
                }

                feedback.textContent = result.isCorrect
                    ? 'Chính xác!'
                    : `Chưa đúng. Đáp án đúng: ${correctChoiceText}.`;
                feedback.className = result.isCorrect
                    ? 'quiz-feedback is-correct'
                    : 'quiz-feedback is-wrong';

                updateAnsweredProgress();
                nextLink.href = result.nextUrl;
                nextLabel.textContent = result.isLastQuestion ? 'Xem kết quả' : 'Câu tiếp theo';
                nextLink.hidden = false;
                root.setAttribute('aria-busy', 'false');
                nextLink.focus();
            } catch (error) {
                showRetryableError();
            }
        });
    });
})();
