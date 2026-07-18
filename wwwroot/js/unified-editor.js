(function () {
    const editor = document.querySelector('.unified-editor');
    if (!editor) return;

    const container = document.getElementById('cards-container');

    function getSetId() {
        const value = editor.dataset.setId;
        return value ? parseInt(value) : null;
    }

    function isNewSet() {
        return !getSetId();
    }
    const setTitleInput = document.getElementById('set-title');
    const saveStatus = document.getElementById('save-status');
    const cardCountLabel = document.getElementById('card-count');
    const btnFinish = document.getElementById('btn-finish');
    const btnAdd = document.getElementById('btn-add-card');

    let pendingSaves = new Map(); // cardId -> timeoutId
    const dirtyCards = new Set(); // card dataset ids with unsaved changes
    let isTitleDirty = false;

    function generateTempId() {
        return 'new-' + crypto.randomUUID();
    }

    function updateCardNumbering() {
        const cards = container.querySelectorAll('.flashcard-card');
        cards.forEach((card, index) => {
            card.querySelector('.card-number').textContent = index + 1;
        });
        cardCountLabel.textContent = cards.length;
    }

    function setSaveStatus(message, type) {
        saveStatus.textContent = message;
        saveStatus.className = 'save-status ' + (type || '');
    }

    function markCardDirty(card) {
        dirtyCards.add(card.dataset.id);
    }

    function markTitleDirty() {
        isTitleDirty = true;
    }

    function getCardData(card) {
        return {
            id: card.dataset.id,
            setId: getSetId() || 0,
            frontText: card.querySelector('.input-front').value,
            backText: card.querySelector('.input-back').value,
            pronunciation: card.querySelector('.input-pronunciation').value,
            partOfSpeech: card.querySelector('.input-part-of-speech').value,
            exampleSentence: card.querySelector('.input-example-sentence').value,
            exampleMeaning: card.querySelector('.input-example-meaning').value,
            synonyms: card.querySelector('.input-synonyms').value,
            imageUrl: null,
            isStarred: card.dataset.starred === 'true'
        };
    }

    function validateCard(data) {
        const errors = [];
        if (!data.frontText.trim()) errors.push('Thuật ngữ không được để trống.');
        if (!data.backText.trim()) errors.push('Định nghĩa không được để trống.');
        return errors;
    }

    async function ensureSetCreated() {
        if (!isNewSet() || getSetId()) return getSetId();

        const title = setTitleInput.value.trim();
        if (!title) return null;

        setSaveStatus('Đang lưu...', 'saving');
        const response = await fetch('/api/flashcards/flashcard-sets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title, description: '', isPublic: false })
        });

        if (!response.ok) {
            setSaveStatus('Lỗi lưu bộ thẻ', 'error');
            return null;
        }

        const set = await response.json();
        editor.dataset.setId = set.id;
        history.replaceState(null, '', `/flashcardset/editor/${set.id}`);
        return set.id;
    }

    async function saveSetMetadata() {
        const title = setTitleInput.value.trim();
        if (!title) return null;

        const setId = getSetId();
        setSaveStatus('Đang lưu...', 'saving');

        const description = editor.dataset.description || '';
        const isPublic = editor.dataset.isPublic === 'true';

        let url = '/api/flashcards/flashcard-sets';
        let method = 'POST';
        if (setId) {
            url = `/api/flashcards/flashcard-sets/${setId}`;
            method = 'PUT';
        }

        try {
            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ title, description, isPublic })
            });

            if (!response.ok) {
                const error = await response.text();
                throw new Error(error);
            }

            if (!setId) {
                const set = await response.json();
                editor.dataset.setId = set.id;
                editor.dataset.description = description;
                editor.dataset.isPublic = isPublic.toString();
                history.replaceState(null, '', `/flashcardset/editor/${set.id}`);
            }

            isTitleDirty = false;
            setSaveStatus('Đã lưu', 'saved');
            return getSetId();
        } catch (err) {
            setSaveStatus('Lỗi lưu bộ thẻ', 'error');
            console.error(err);
            return null;
        }
    }

    async function saveCard(card) {
        const originalId = card.dataset.id;

        if (pendingSaves.has(originalId)) {
            clearTimeout(pendingSaves.get(originalId));
            pendingSaves.delete(originalId);
        }

        const data = getCardData(card);
        const errors = validateCard(data);
        if (errors.length > 0) {
            showCardErrors(card, errors);
            return;
        }
        clearCardErrors(card);

        const currentSetId = await ensureSetCreated();
        if (!currentSetId) return;

        data.setId = currentSetId;
        card.dataset.setId = currentSetId;
        setSaveStatus('Đang lưu...', 'saving');

        const isNewCard = data.id.startsWith('new-');
        const url = isNewCard
            ? '/api/flashcards/flashcards'
            : `/api/flashcards/flashcards/${data.id}`;
        const method = isNewCard ? 'POST' : 'PUT';

        try {
            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            });

            if (!response.ok) {
                const error = await response.text();
                throw new Error(error);
            }

            if (isNewCard) {
                const created = await response.json();
                card.dataset.id = created.id.toString();
                pendingSaves.delete(originalId);
            }

            setSaveStatus('Đã lưu', 'saved');
            dirtyCards.delete(originalId);
            dirtyCards.delete(card.dataset.id);
        } catch (err) {
            setSaveStatus('Lỗi lưu', 'error');
            card.classList.add('card-error');
            console.error(err);
        }
    }

    function scheduleSave(card) {
        const id = card.dataset.id;
        if (pendingSaves.has(id)) {
            clearTimeout(pendingSaves.get(id));
        }
        markCardDirty(card);
        setSaveStatus('Đang chờ lưu...', 'pending');
        const timeoutId = setTimeout(() => saveCard(card), 1500);
        pendingSaves.set(id, timeoutId);
    }

    function showCardErrors(card, errors) {
        let errorBox = card.querySelector('.card-errors');
        if (!errorBox) {
            errorBox = document.createElement('div');
            errorBox.className = 'card-errors';
            card.querySelector('.card-body').prepend(errorBox);
        }
        errorBox.innerHTML = errors.map(e => `<div class="error">${e}</div>`).join('');
    }

    function clearCardErrors(card) {
        const errorBox = card.querySelector('.card-errors');
        if (errorBox) errorBox.remove();
        card.classList.remove('card-error');
    }

    function createEmptyCard() {
        const tempId = generateTempId();
        const div = document.createElement('div');
        div.className = 'flashcard-card expanded';
        div.dataset.id = tempId;
        div.dataset.starred = 'false';
        div.innerHTML = `
            <div class="card-header">
                <span class="card-number">0</span>
                <button type="button" class="btn-star" aria-label="Toggle star">☆</button>
                <span class="card-term"></span>
                <div class="card-actions">
                    <button type="button" class="btn-toggle" aria-label="Expand/collapse">▲</button>
                    <button type="button" class="btn-delete" aria-label="Delete">🗑</button>
                </div>
            </div>
            <div class="card-body">
                <div class="form-row">
                    <div class="form-group">
                        <label>Thuật ngữ <span class="required">*</span></label>
                        <input class="form-control input-front" placeholder="Thuật ngữ" />
                    </div>
                    <div class="form-group">
                        <label>Định nghĩa <span class="required">*</span></label>
                        <input class="form-control input-back" placeholder="Định nghĩa" />
                    </div>
                </div>
                <div class="form-row">
                    <div class="form-group">
                        <label>Phát âm</label>
                        <input class="form-control input-pronunciation" placeholder="IPA" />
                    </div>
                    <div class="form-group">
                        <label>Loại từ</label>
                        <input class="form-control input-part-of-speech" placeholder="noun, verb..." />
                    </div>
                </div>
                <div class="form-group">
                    <label>Ví dụ tiếng Anh</label>
                    <textarea class="form-control input-example-sentence" rows="2" placeholder="Ví dụ"></textarea>
                </div>
                <div class="form-group">
                    <label>Nghĩa câu ví dụ tiếng Việt</label>
                    <textarea class="form-control input-example-meaning" rows="2" placeholder="Nghĩa"></textarea>
                </div>
                <div class="form-group">
                    <label>Từ đồng nghĩa</label>
                    <input class="form-control input-synonyms" placeholder="Cách nhau bằng dấu phẩy" />
                </div>
            </div>
        `;
        bindCardEvents(div);
        return div;
    }

    function bindCardEvents(card) {
        const inputs = card.querySelectorAll('input, textarea');
        inputs.forEach(input => {
            input.addEventListener('input', () => {
                if (input.classList.contains('input-front')) {
                    card.querySelector('.card-term').textContent = input.value;
                }
                scheduleSave(card);
            });
            input.addEventListener('blur', () => saveCard(card));
        });

        card.querySelector('.btn-toggle').addEventListener('click', (e) => {
            e.stopPropagation();
            card.classList.toggle('expanded');
            card.classList.toggle('collapsed');
            card.querySelector('.btn-toggle').textContent = card.classList.contains('expanded') ? '▲' : '▼';
        });

        card.querySelector('.btn-delete').addEventListener('click', async (e) => {
            e.stopPropagation();
            if (!confirm('Xóa thẻ này?')) return;

            const id = card.dataset.id;
            if (pendingSaves.has(id)) {
                clearTimeout(pendingSaves.get(id));
                pendingSaves.delete(id);
            }
            dirtyCards.delete(id);
            if (!id.startsWith('new-')) {
                await fetch(`/api/flashcards/flashcards/${id}`, { method: 'DELETE' });
            }
            card.remove();
            updateCardNumbering();
        });

        card.querySelector('.btn-star').addEventListener('click', async (e) => {
            e.stopPropagation();
            const id = card.dataset.id;
            if (id.startsWith('new-')) return;

            const response = await fetch(`/api/flashcards/flashcards/${id}/star`, { method: 'POST' });
            if (response.ok) {
                const result = await response.json();
                card.dataset.starred = result.isStarred;
                card.querySelector('.btn-star').textContent = result.isStarred ? '★' : '☆';
            }
        });

        card.addEventListener('click', () => {
            if (!card.classList.contains('expanded')) {
                card.classList.add('expanded');
                card.classList.remove('collapsed');
                card.querySelector('.btn-toggle').textContent = '▲';
            }
        });
    }

    setTitleInput.addEventListener('input', () => {
        btnFinish.disabled = !setTitleInput.value.trim();
        markTitleDirty();
    });
    setTitleInput.addEventListener('blur', async () => {
        if (setTitleInput.value.trim()) {
            await saveSetMetadata();
        }
    });

    btnAdd.addEventListener('click', () => {
        const card = createEmptyCard();
        container.appendChild(card);
        updateCardNumbering();
        card.querySelector('.input-front').focus();
    });

    btnFinish.addEventListener('click', () => {
        window.location.href = '/Set';
    });

    container.querySelectorAll('.flashcard-card').forEach(bindCardEvents);
    updateCardNumbering();
    btnFinish.disabled = !setTitleInput.value.trim();

    window.addEventListener('beforeunload', (e) => {
        if (dirtyCards.size > 0 || isTitleDirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    });
})();
