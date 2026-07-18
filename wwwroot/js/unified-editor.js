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

    async function persistOrder() {
        try {
            const orderedIds = Array.from(container.querySelectorAll('.flashcard-card'))
                .map(card => parseInt(card.dataset.id))
                .filter(id => !isNaN(id));

            if (orderedIds.length === 0) return true;

            const currentSetId = getSetId() || await ensureSetCreated();
            if (!currentSetId) return false;

            setSaveStatus('Đang lưu...', 'saving');
            const response = await fetch('/api/flashcards/reorder', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ setId: currentSetId, orderedCardIds: orderedIds })
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            updateCardNumbering();
            setSaveStatus('Đã lưu', 'saved');
            return true;
        } catch (err) {
            setSaveStatus('Lỗi lưu thứ tự', 'error');
            console.error('Reorder failed:', err);
            return false;
        }
    }

    let orderBeforeDrag = [];
    const sortable = Sortable.create(container, {
        handle: '.card-drag-handle',
        animation: 150,
        ghostClass: 'sortable-ghost',
        onStart: function () {
            orderBeforeDrag = sortable.toArray();
        },
        onEnd: async function (evt) {
            const ok = await persistOrder();
            if (!ok && evt) {
                sortable.sort(orderBeforeDrag);
                updateCardNumbering();
            }
            return ok;
        }
    });

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
                <span class="card-drag-handle" aria-label="Drag to reorder">⋮⋮</span>
                <span class="card-number">0</span>
                <button type="button" class="btn-star" aria-label="Toggle star">☆</button>
                <span class="card-term"></span>
                <div class="card-actions">
                    <button type="button" class="btn-move-up" aria-label="Move up">↑</button>
                    <button type="button" class="btn-move-down" aria-label="Move down">↓</button>
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

        card.querySelector('.btn-move-up')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            const prev = card.previousElementSibling;
            if (!prev) return;
            container.insertBefore(card, prev);
            try {
                const ok = await sortable.options.onEnd();
                if (!ok) {
                    container.insertBefore(prev, card);
                }
            } catch (err) {
                container.insertBefore(prev, card);
            }
        });

        card.querySelector('.btn-move-down')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            const next = card.nextElementSibling;
            if (!next) return;
            container.insertBefore(next, card);
            try {
                const ok = await sortable.options.onEnd();
                if (!ok) {
                    container.insertBefore(card, next);
                }
            } catch (err) {
                container.insertBefore(card, next);
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

    const btnImport = document.getElementById('btn-import');
    const importModal = document.getElementById('import-modal');
    const importText = document.getElementById('import-text');
    const importDelimiter = document.getElementById('import-delimiter');
    const importReplace = document.getElementById('import-replace');
    const importPreview = document.getElementById('import-preview');
    const btnImportCancel = document.getElementById('btn-import-cancel');
    const btnImportConfirm = document.getElementById('btn-import-confirm');

    function parseImportText(text, delimiter) {
        return text.split('\n')
            .map(line => line.trim())
            .filter(line => line.length > 0)
            .map(line => {
                const parts = line.split(delimiter);
                return {
                    frontText: parts[0]?.trim() ?? '',
                    backText: parts[1]?.trim() ?? ''
                };
            });
    }

    function renderPreview(rows) {
        const valid = rows.filter(r => r.frontText && r.backText);
        const invalid = rows.filter(r => !r.frontText || !r.backText);
        importPreview.innerHTML = `
            <p>Hợp lệ: ${valid.length}, lỗi: ${invalid.length}</p>
            <ul>
                ${valid.slice(0, 5).map(r => `<li>${r.frontText} → ${r.backText}</li>`).join('')}
                ${invalid.map(r => `<li class="error">Lỗi: "${r.frontText}" / "${r.backText}"</li>`).join('')}
            </ul>
        `;
    }

    btnImport.addEventListener('click', () => {
        importModal.style.display = 'flex';
        importText.value = '';
        importPreview.innerHTML = '';
    });

    btnImportCancel.addEventListener('click', () => {
        importModal.style.display = 'none';
    });

    [importText, importDelimiter].forEach(el => {
        el.addEventListener('input', () => {
            const rows = parseImportText(importText.value, importDelimiter.value);
            renderPreview(rows);
        });
    });

    btnImportConfirm.addEventListener('click', async () => {
        const rows = parseImportText(importText.value, importDelimiter.value)
            .filter(r => r.frontText && r.backText);
        if (rows.length === 0) return;

        const currentSetId = getSetId() || await ensureSetCreated();
        if (!currentSetId) return;

        const payload = {
            setId: currentSetId,
            cards: rows.map(r => ({
                setId: currentSetId,
                frontText: r.frontText,
                backText: r.backText,
                isStarred: false
            })),
            replaceAll: importReplace.checked
        };

        if (importReplace.checked) {
            container.querySelectorAll('.flashcard-card').forEach(c => c.remove());
        }

        const response = await fetch('/api/flashcards/flashcards/batch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            const created = await response.json();
            created.forEach(c => {
                const card = createEmptyCard();
                card.dataset.id = c.id;
                card.querySelector('.input-front').value = c.frontText;
                card.querySelector('.input-back').value = c.backText;
                card.querySelector('.card-term').textContent = c.frontText;
                container.appendChild(card);
            });
            updateCardNumbering();
            importModal.style.display = 'none';
        }
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
