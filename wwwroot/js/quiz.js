(() => {
    'use strict';

    const root = document.querySelector('[data-quiz-root]');
    if (!root) return;

    const buttons = Array.from(root.querySelectorAll('[data-quiz-answer]'));
    const feedback = root.querySelector('[data-quiz-feedback]');
    const nextLink = root.querySelector('[data-quiz-next]');
    const nextLabel = root.querySelector('[data-quiz-next-label]');
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

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

                correctButton.classList.add('is-correct');
                if (result.isCorrect === false) button.classList.add('is-wrong');

                feedback.textContent = result.isCorrect
                    ? 'Chính xác!'
                    : 'Chưa đúng. Đáp án đúng đã được đánh dấu.';
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
