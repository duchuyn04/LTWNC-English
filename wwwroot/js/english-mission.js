(function () {
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
    var pendingText = '';
    var pendingTurnId = '';
    var busy = false;

    function escapeText(value) {
        var node = document.createElement('div');
        node.textContent = value || '';
        return node.innerHTML;
    }

    function appendTurn(turn) {
        var user = document.createElement('div');
        user.className = 'mission-message mission-message-user';
        user.innerHTML = '<div><small>Bạn</small><p lang="en">' + escapeText(turn.userText) + '</p></div><span class="mission-avatar mission-avatar-user">YOU</span>';
        transcript.appendChild(user);

        var npc = document.createElement('div');
        npc.className = 'mission-message mission-message-npc';
        var detail = '<small>' + escapeText(npcName) + '</small><p lang="en">' + escapeText(turn.npcText) + '</p>';
        if (turn.feedbackVi) detail += '<span class="mission-message-note"><i class="ph ph-check-circle"></i> ' + escapeText(turn.feedbackVi) + '</span>';
        if (turn.correctionEn) detail += '<div class="mission-correction"><strong>Tự nhiên hơn</strong><span lang="en">' + escapeText(turn.correctionEn) + '</span><small>' + escapeText(turn.correctionExplanationVi) + '</small></div>';
        npc.innerHTML = '<span class="mission-avatar">AI</span><div>' + detail + '</div>';
        transcript.appendChild(npc);
        npc.scrollIntoView({ behavior: 'smooth', block: 'end' });
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
        send.disabled = true;
        send.classList.add('is-loading');
        errorBox.hidden = true;

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
            appendTurn(data.turn);
            updateWords(data.targetWords);
            var count = document.getElementById('mission-turn-count');
            count.textContent = String(Number(count.textContent) + 1);
            input.value = '';
            pendingText = '';
            pendingTurnId = '';
            if (data.completed) window.location.href = data.resultUrl;
        } catch (error) {
            errorBox.querySelector('p').textContent = error.message;
            errorBox.hidden = false;
            input.value = pendingText;
        } finally {
            busy = false;
            send.disabled = false;
            send.classList.remove('is-loading');
            input.focus();
        }
    }

    send.addEventListener('click', submit);
    retry.addEventListener('click', submit);
    input.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); submit(); }
    });
    page.addEventListener('click', function (event) {
        var button = event.target.closest('.mission-play');
        if (!button || !window.speechSynthesis) return;
        var text = button.closest('.mission-message').querySelector('p').textContent;
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(new SpeechSynthesisUtterance(text));
    });
})();
