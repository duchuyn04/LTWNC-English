(function () {
    var topicPage = document.querySelector('[data-mission-topic-page]');

    function configureTopicStart() {
        if (!topicPage) return;
        var forms = topicPage.querySelectorAll('[data-mission-start-form]');
        var status = topicPage.querySelector('[data-mission-start-status]');
        var skeleton = topicPage.querySelector('[data-mission-start-skeleton]');

        forms.forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (topicPage.getAttribute('aria-busy') === 'true') {
                    event.preventDefault();
                    return;
                }

                topicPage.setAttribute('aria-busy', 'true');
                forms.forEach(function (item) {
                    var button = item.querySelector('button[type="submit"]');
                    button.disabled = true;
                });

                var selectedButton = form.querySelector('button[type="submit"]');
                selectedButton.classList.add('is-loading');
                selectedButton.querySelector('span').textContent = 'Đang tạo';
                selectedButton.querySelector('i').className = 'ph ph-circle-notch';
                status.hidden = false;
                skeleton.hidden = false;
                topicPage.classList.add('is-starting');
            });
        });
    }

    configureTopicStart();

    var page = document.querySelector('.mission-chat-page');
    if (!page) return;

    var setId = page.dataset.setId;
    var sessionId = page.dataset.sessionId;
    var npcName = page.dataset.npcName;
    var token = page.querySelector('input[name="__RequestVerificationToken"]').value;
    var input = document.getElementById('mission-answer');
    var send = document.getElementById('mission-send');
    var retry = document.getElementById('mission-retry');
    var errorBox = document.getElementById('mission-error');
    var transcript = document.getElementById('mission-transcript');
    var progress = page.querySelector('[data-mission-progress]');
    var progressBar = page.querySelector('[data-mission-progress-bar]');
    var suggestion = page.querySelector('[data-mission-suggestion]');
    var pendingText = '';
    var pendingTurnId = '';
    var pendingUserNode = null;
    var pendingNpcNode = null;
    var busy = false;

    function escapeText(value) {
        var node = document.createElement('div');
        node.textContent = value || '';
        return node.innerHTML;
    }

    function appendUserTurn(userText) {
        if (pendingUserNode) return pendingUserNode;
        var user = document.createElement('div');
        user.className = 'mission-message mission-message-user';
        user.innerHTML = '<div><small>Bạn</small><p lang="en">' + escapeText(userText) + '</p></div><span class="mission-avatar mission-avatar-user" aria-hidden="true">YOU</span>';
        transcript.appendChild(user);
        pendingUserNode = user;
        return user;
    }

    function speakText(text) {
        if (!text || !window.speechSynthesis || !window.SpeechSynthesisUtterance) return;
        var utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'en-US';
        utterance.rate = 0.92;
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(utterance);
    }

    function updateSuggestion(english, vietnamese) {
        if (!suggestion) return;
        suggestion.querySelector('[data-suggestion-en]').textContent = english || '';
        suggestion.querySelector('[data-suggestion-vi]').textContent = vietnamese || '';
        suggestion.classList.toggle('is-empty', !english || !vietnamese);
    }

    function appendPendingNpc() {
        if (pendingNpcNode) return pendingNpcNode;
        var npc = document.createElement('div');
        npc.className = 'mission-message mission-message-npc mission-message-pending';
        npc.innerHTML = '<span class="mission-avatar" aria-hidden="true">AI</span><div><small>'
            + escapeText(npcName)
            + '</small><div class="mission-typing-dots" role="status" aria-label="AI đang trả lời"><span></span><span></span><span></span></div></div>';
        transcript.appendChild(npc);
        pendingNpcNode = npc;
        npc.scrollIntoView({ behavior: 'smooth', block: 'end' });
        return npc;
    }

    async function streamNpcText(element, text) {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            element.textContent = text;
            return;
        }

        var chunks = text.match(/\S+\s*/g) || [text];
        element.textContent = '';
        for (var index = 0; index < chunks.length; index += 1) {
            element.textContent += chunks[index];
            await new Promise(function (resolve) { window.setTimeout(resolve, 34); });
        }
    }

    async function appendTurn(turn) {
        var npc = appendPendingNpc();
        npc.classList.remove('mission-message-pending');
        var container = npc.querySelector('div');
        container.innerHTML = '<small>' + escapeText(npcName) + '</small><p lang="en"></p>';
        speakText(turn.npcText);
        await streamNpcText(container.querySelector('p'), turn.npcText);

        var detail = '<button class="mission-play" type="button" aria-label="Nghe lại câu của '
            + escapeText(npcName)
            + '"><i class="ph ph-speaker-high" aria-hidden="true"></i> Nghe lại</button>';
        if (turn.feedbackVi) detail += '<span class="mission-message-note"><i class="ph ph-check-circle" aria-hidden="true"></i> ' + escapeText(turn.feedbackVi) + '</span>';
        if (turn.correctionEn) detail += '<div class="mission-correction"><strong>Tự nhiên hơn</strong><span lang="en">' + escapeText(turn.correctionEn) + '</span><small>' + escapeText(turn.correctionExplanationVi) + '</small></div>';
        container.insertAdjacentHTML('beforeend', detail);
        configureSpeechButtons();
        pendingUserNode = null;
        pendingNpcNode = null;
        npc.scrollIntoView({
            behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
            block: 'end'
        });
    }

    function updateProgress(answered) {
        if (!progress || !progressBar) return;
        var total = Number(progress.getAttribute('aria-valuemax'));
        if (!Number.isFinite(total)) return;
        var value = Math.min(total, answered);
        progress.setAttribute('aria-valuenow', String(value));
        progressBar.style.width = (total > 0 ? value * 100 / total : 0) + '%';
    }

    function configureSpeechButtons() {
        var supported = Boolean(window.speechSynthesis && window.SpeechSynthesisUtterance);
        page.querySelectorAll('.mission-play').forEach(function (button) {
            button.hidden = !supported;
        });
    }

    function updateWords(words) {
        words.forEach(function (word) {
            var chip = page.querySelector('.mission-word-chip[data-word="' + CSS.escape(word.term) + '"]');
            if (!chip || !word.isUsed) return;
            chip.classList.add('is-used');
            if (!chip.querySelector('i')) chip.insertAdjacentHTML('beforeend', '<i class="ph ph-check"></i>');
        });
    }

    async function submit() {
        if (busy) return;
        var value = (pendingText || input.value).trim();
        if (!value) return;
        pendingText = value;
        if (!pendingTurnId) pendingTurnId = window.crypto && window.crypto.randomUUID ? window.crypto.randomUUID() : String(Date.now()) + '-' + Math.random().toString(16).slice(2);
        busy = true;
        page.setAttribute('aria-busy', 'true');
        send.disabled = true;
        input.disabled = true;
        send.classList.add('is-loading');
        errorBox.hidden = true;
        appendUserTurn(value);
        appendPendingNpc();
        input.value = '';

        var body = new URLSearchParams();
        body.append('__RequestVerificationToken', token);
        body.append('userText', value);
        body.append('clientTurnId', pendingTurnId);
        try {
            var response = await fetch('/Study/' + setId + '/Mission/' + sessionId + '/Respond', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest' },
                body: body.toString()
            });
            var data = await response.json();
            if (!response.ok) throw new Error(data.error || 'Không thể gửi câu trả lời.');
            await appendTurn(data.turn);
            updateWords(data.targetWords);
            updateSuggestion(data.suggestedReplyEn, data.suggestedReplyVi);
            var count = document.getElementById('mission-turn-count');
            var nextCount = Number(count.textContent) + 1;
            count.textContent = String(nextCount);
            updateProgress(nextCount);
            input.value = '';
            pendingText = '';
            pendingTurnId = '';
            if (data.completed) window.location.href = data.resultUrl;
        } catch (error) {
            if (pendingNpcNode) pendingNpcNode.remove();
            pendingNpcNode = null;
            errorBox.querySelector('p').textContent = error.message;
            errorBox.hidden = false;
            input.value = pendingText;
        } finally {
            busy = false;
            page.setAttribute('aria-busy', 'false');
            send.disabled = false;
            input.disabled = false;
            send.classList.remove('is-loading');
            input.focus();
        }
    }

    send.addEventListener('click', submit);
    retry.addEventListener('click', submit);
    configureSpeechButtons();
    speakText(page.dataset.openingLine);
    input.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); submit(); }
    });
    page.addEventListener('click', function (event) {
        var button = event.target.closest('.mission-play');
        if (!button) return;
        var text = button.closest('.mission-message').querySelector('p').textContent;
        speakText(text);
    });
    if (suggestion) {
        suggestion.addEventListener('click', function () {
            var text = suggestion.querySelector('[data-suggestion-en]').textContent.trim();
            if (!text) return;
            input.value = text;
            input.focus();
            input.setSelectionRange(text.length, text.length);
        });
    }
})();
