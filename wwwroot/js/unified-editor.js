(function () {
    const editor = document.querySelector('.unified-editor');
    if (!editor) return;

    const antiforgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    function apiFetch(url, options) {
        const requestOptions = options || {};
        const headers = new Headers(requestOptions.headers || {});
        headers.set('RequestVerificationToken', antiforgeryToken);
        return fetch(url, { ...requestOptions, headers });
    }

    const container = document.getElementById('cards-container');

    function getSetId() {
        const value = editor.dataset.setId;
        return value ? parseInt(value) : null;
    }

    function isNewSet() {
        return !getSetId();
    }
    const setTitleInput = document.getElementById('set-title');
    const setDescriptionInput = document.getElementById('set-description');
    const setIsPublicInput = document.getElementById('set-is-public');
    const saveStatus = document.getElementById('save-status');
    const cardCountLabel = document.getElementById('card-count');
    const btnFinish = document.getElementById('btn-finish');
    const quickActions = document.querySelector('.editor-quick-actions');
    const quickSaveLabel = document.getElementById('editor-quick-save-label');
    const quickCardCount = document.getElementById('editor-quick-card-count');
    const btnFinishSticky = document.getElementById('btn-finish-sticky');
    const btnAdd = document.getElementById('btn-add-card');

    let pendingSaves = new Map(); // cardId -> timeoutId
    const dirtyCards = new Set(); // card dataset ids with unsaved changes
    let isTitleDirty = false;

    let tempIdCounter = 0;

    function generateTempId() {
        // crypto.randomUUID() chỉ có trong secure context (HTTPS); fallback cho HTTP.
        if (window.crypto && typeof window.crypto.randomUUID === 'function') {
            return 'new-' + crypto.randomUUID();
        }
        tempIdCounter += 1;
        return 'new-' + Date.now().toString(36) + '-' + tempIdCounter;
    }

    function updateCardNumbering() {
        const cards = container.querySelectorAll('.flashcard-card');
        cards.forEach((card, index) => {
            card.querySelector('.card-number').textContent = index + 1;
        });
        cardCountLabel.textContent = cards.length;
        if (quickCardCount) quickCardCount.textContent = cards.length;
    }

    async function persistOrder() {
        try {
            const currentSetId = getSetId() || await ensureSetCreated();
            if (!currentSetId) return false;

            // Save any unsaved new cards so their temp ids become real numeric ids.
            const newCards = Array.from(container.querySelectorAll('.flashcard-card'))
                .filter(card => card.dataset.id.startsWith('new-'));
            for (const card of newCards) {
                if (pendingSaves.has(card.dataset.id)) {
                    clearTimeout(pendingSaves.get(card.dataset.id));
                    pendingSaves.delete(card.dataset.id);
                }
                await saveCard(card);
            }

            const orderedIds = Array.from(container.querySelectorAll('.flashcard-card'))
                .map(card => parseInt(card.dataset.id))
                .filter(id => !isNaN(id));

            if (orderedIds.length === 0) return true;

            setSaveStatus('Đang lưu...', 'saving');
            const response = await apiFetch('/api/flashcards/flashcards/reorder', {
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

    // Snapshot các element thẻ theo thứ tự DOM trước khi kéo — revert bằng chính
    // element, an toàn khi temp id (new-...) đổi thành id thật sau khi lưu.
    let orderBeforeDrag = [];
    const sortable = Sortable.create(container, {
        handle: '.card-drag-handle',
        animation: 150,
        ghostClass: 'sortable-ghost',
        onStart: function () {
            orderBeforeDrag = Array.from(container.querySelectorAll('.flashcard-card'));
        },
        onEnd: async function (evt) {
            const ok = await persistOrder();
            if (!ok && evt) {
                orderBeforeDrag.forEach(el => container.appendChild(el));
                updateCardNumbering();
            }
            return ok;
        }
    });

    function setSaveStatus(message, type) {
        saveStatus.textContent = message;
        saveStatus.className = 'save-status ' + (type || '');
        if (quickSaveLabel) quickSaveLabel.textContent = message || 'Đã tự động lưu';
        if (quickActions) quickActions.dataset.state = type || 'saved';
    }

    function markCardDirty(card) {
        dirtyCards.add(card.dataset.id);
    }

    function markTitleDirty() {
        isTitleDirty = true;
    }

    function getSetMetadata() {
        return {
            title: setTitleInput.value.trim(),
            description: setDescriptionInput.value.trim(),
            isPublic: setIsPublicInput.checked
        };
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

    // Promise dùng chung để serialize việc tạo set mới — tránh race khi
    // saveCard() và saveSetMetadata() cùng POST tạo set một lúc (tạo trùng set).
    let setCreationPromise = null;

    async function ensureSetCreated() {
        const existingId = getSetId();
        if (existingId) return existingId;
        if (setCreationPromise) return setCreationPromise;

        const metadata = getSetMetadata();
        if (!metadata.title) return null;

        setSaveStatus('Đang lưu...', 'saving');
        setCreationPromise = (async () => {
            try {
                const response = await apiFetch('/api/flashcards/flashcard-sets', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(metadata)
                });

                if (!response.ok) {
                    setSaveStatus('Lỗi lưu bộ thẻ', 'error');
                    return null;
                }

                const set = await response.json();
                editor.dataset.setId = set.id;
                editor.dataset.description = metadata.description;
                editor.dataset.isPublic = metadata.isPublic.toString();
                history.replaceState(null, '', `/flashcardset/editor/${set.id}`);
                return set.id;
            } finally {
                setCreationPromise = null;
            }
        })();
        return setCreationPromise;
    }

    async function saveSetMetadata() {
        const metadata = getSetMetadata();
        if (!metadata.title) return null;

        // Set mới: tạo qua ensureSetCreated (đã serialize) rồi PUT metadata mới nhất.
        const setId = getSetId() || await ensureSetCreated();
        if (!setId) return null;

        setSaveStatus('Đang lưu...', 'saving');

        try {
            const response = await apiFetch(`/api/flashcards/flashcard-sets/${setId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(metadata)
            });

            if (!response.ok) {
                const error = await response.text();
                throw new Error(error);
            }

            editor.dataset.description = metadata.description;
            editor.dataset.isPublic = metadata.isPublic.toString();
            isTitleDirty = false;
            setSaveStatus('Đã lưu', 'saved');
            return getSetId();
        } catch (err) {
            setSaveStatus('Lỗi lưu bộ thẻ', 'error');
            console.error(err);
            return null;
        }
    }

    // Serialize các lần lưu của cùng một thẻ qua chuỗi promise — tránh race
    // tạo trùng thẻ khi debounce và blur cùng POST lúc thẻ chưa có id thật.
    function saveCard(card) {
        const originalId = card.dataset.id;
        if (pendingSaves.has(originalId)) {
            clearTimeout(pendingSaves.get(originalId));
            pendingSaves.delete(originalId);
        }

        const queued = (card.savePromise || Promise.resolve()).then(() => persistCard(card));
        card.savePromise = queued.catch(() => {});
        return queued;
    }

    async function persistCard(card) {
        const originalId = card.dataset.id;

        const data = getCardData(card);
        const errors = validateCard(data);
        if (errors.length > 0) {
            // Blur/debounce saves stay quiet for incomplete cards. The full
            // validation summary is shown only when the user finishes editing.
            return false;
        }
        clearCardErrors(card);

        const currentSetId = await ensureSetCreated();
        if (!currentSetId) return false;

        data.setId = currentSetId;
        card.dataset.setId = currentSetId;
        setSaveStatus('Đang lưu...', 'saving');

        const isNewCard = !data.id || data.id === '0' || data.id.startsWith('new-');
        const url = isNewCard
            ? '/api/flashcards/flashcards'
            : `/api/flashcards/flashcards/${data.id}`;
        const method = isNewCard ? 'POST' : 'PUT';

        try {
            const response = await apiFetch(url, {
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
            return true;
        } catch (err) {
            setSaveStatus('Lỗi lưu', 'error');
            card.classList.add('card-error');
            console.error(err);
            return false;
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
                    <button type="button" class="btn-move-up" aria-label="Đưa thẻ lên" title="Đưa thẻ lên">
                        <i class="ph ph-caret-up" aria-hidden="true"></i>
                    </button>
                    <button type="button" class="btn-move-down" aria-label="Đưa thẻ xuống" title="Đưa thẻ xuống">
                        <i class="ph ph-caret-down" aria-hidden="true"></i>
                    </button>
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
                try {
                    const response = await apiFetch(`/api/flashcards/flashcards/${id}`, { method: 'DELETE' });
                    if (!response.ok) {
                        throw new Error(`HTTP ${response.status}`);
                    }
                } catch (err) {
                    setSaveStatus('Lỗi xóa thẻ', 'error');
                    console.error('Delete failed:', err);
                    return;
                }
            }
            card.remove();
            updateCardNumbering();
        });

        card.querySelector('.btn-star').addEventListener('click', async (e) => {
            e.stopPropagation();
            const id = card.dataset.id;
            if (id.startsWith('new-')) return;

            const starButton = card.querySelector('.btn-star');
            const previousState = card.dataset.starred === 'true';
            try {
                const response = await apiFetch(`/api/flashcards/flashcards/${id}/star`, { method: 'POST' });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                const result = await response.json();
                card.dataset.starred = result.isStarred;
                starButton.textContent = result.isStarred ? '★' : '☆';
            } catch (err) {
                setSaveStatus('Lỗi đánh sao', 'error');
                card.dataset.starred = previousState ? 'true' : 'false';
                starButton.textContent = previousState ? '★' : '☆';
                console.error('Star toggle failed:', err);
            }
        });

        // Nút lên/xuống: tự revert bằng DOM, không đi qua onEnd của sortable
        // (onEnd dùng orderBeforeDrag của lần kéo trước — revert sai thứ tự).
        card.querySelector('.btn-move-up')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            const prev = card.previousElementSibling;
            if (!prev) return;
            container.insertBefore(card, prev);
            updateCardNumbering();
            const ok = await persistOrder();
            if (!ok) {
                container.insertBefore(prev, card);
                updateCardNumbering();
            }
        });

        card.querySelector('.btn-move-down')?.addEventListener('click', async (e) => {
            e.stopPropagation();
            const next = card.nextElementSibling;
            if (!next) return;
            container.insertBefore(next, card);
            updateCardNumbering();
            const ok = await persistOrder();
            if (!ok) {
                container.insertBefore(card, next);
                updateCardNumbering();
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

    function syncFinishButtons() {
        const disabled = !setTitleInput.value.trim();
        btnFinish.disabled = disabled;
        if (btnFinishSticky) btnFinishSticky.disabled = disabled;
    }

    setTitleInput.addEventListener('input', () => {
        syncFinishButtons();
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

    function validateAllCards() {
        let firstInvalidCard = null;

        container.querySelectorAll('.flashcard-card').forEach(card => {
            const data = getCardData(card);
            const errors = validateCard(data);

            if (errors.length > 0) {
                showCardErrors(card, errors);
                firstInvalidCard ??= card;
            } else {
                clearCardErrors(card);
            }
        });

        if (!firstInvalidCard) return true;

        const firstFrontInput = firstInvalidCard.querySelector('.input-front');
        const firstBackInput = firstInvalidCard.querySelector('.input-back');
        const focusTarget = firstFrontInput?.value.trim()
            ? firstBackInput
            : firstFrontInput;

        firstInvalidCard.classList.add('expanded');
        firstInvalidCard.classList.remove('collapsed');
        firstInvalidCard.scrollIntoView({ behavior: 'smooth', block: 'center' });
        focusTarget?.focus();
        setSaveStatus('Bổ sung thuật ngữ và định nghĩa trước khi hoàn tất', 'error');
        return false;
    }

    let isFinishing = false;
    async function finishEditor() {
        if (isFinishing || !validateAllCards()) return;
        isFinishing = true;
        btnFinish.disabled = true;
        if (btnFinishSticky) btnFinishSticky.disabled = true;

        const cards = Array.from(container.querySelectorAll('.flashcard-card'));
        await Promise.all(cards.map(card => saveCard(card)));

        if (isTitleDirty) {
            await saveSetMetadata();
        }

        const hasCardSaveErrors = cards.some(card => card.classList.contains('card-error'));
        if (hasCardSaveErrors || isTitleDirty) {
            isFinishing = false;
            syncFinishButtons();
            setSaveStatus('Chưa thể hoàn tất. Kiểm tra lại trạng thái lưu.', 'error');
            return;
        }

        window.location.href = '/Set';
    }

    btnFinish.addEventListener('click', finishEditor);
    btnFinishSticky?.addEventListener('click', finishEditor);

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

        // Dựng bằng DOM/textContent — nội dung paste từ ngưới dùng, tránh XSS qua innerHTML.
        importPreview.innerHTML = '';
        const summary = document.createElement('p');
        summary.textContent = `Hợp lệ: ${valid.length}, lỗi: ${invalid.length}`;
        importPreview.appendChild(summary);

        const list = document.createElement('ul');
        valid.slice(0, 5).forEach(r => {
            const li = document.createElement('li');
            li.textContent = `${r.frontText} · ${r.backText}`;
            list.appendChild(li);
        });
        invalid.forEach(r => {
            const li = document.createElement('li');
            li.className = 'error';
            li.textContent = `Lỗi: "${r.frontText}" / "${r.backText}"`;
            list.appendChild(li);
        });
        importPreview.appendChild(list);
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

        const replaceAll = importReplace.checked;
        const existingCards = Array.from(container.querySelectorAll('.flashcard-card'));

        const payload = {
            setId: currentSetId,
            cards: rows.map(r => ({
                setId: currentSetId,
                frontText: r.frontText,
                backText: r.backText,
                isStarred: false
            })),
            replaceAll
        };

        setSaveStatus('Đang import...', 'saving');
        try {
            const response = await apiFetch('/api/flashcards/flashcards/batch', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                const created = await response.json();

                if (replaceAll) {
                    existingCards.forEach(c => {
                        if (pendingSaves.has(c.dataset.id)) {
                            clearTimeout(pendingSaves.get(c.dataset.id));
                            pendingSaves.delete(c.dataset.id);
                        }
                        dirtyCards.delete(c.dataset.id);
                        c.remove();
                    });
                }

                created.forEach(c => {
                    const card = createEmptyCard();
                    card.dataset.id = c.id;
                    card.querySelector('.input-front').value = c.frontText;
                    card.querySelector('.input-back').value = c.backText;
                    card.querySelector('.card-term').textContent = c.frontText;
                    container.appendChild(card);
                });
                updateCardNumbering();
                setSaveStatus('Đã import', 'saved');
                importModal.style.display = 'none';
            } else {
                const errorText = await response.text().catch(() => 'Import thất bại');
                setSaveStatus('Lỗi import: ' + errorText, 'error');
                console.error('Batch import failed:', errorText);
            }
        } catch (err) {
            setSaveStatus('Lỗi import', 'error');
            console.error('Batch import failed:', err);
        }
    });

    container.querySelectorAll('.flashcard-card').forEach(bindCardEvents);
    updateCardNumbering();
    syncFinishButtons();

    window.addEventListener('beforeunload', (e) => {
        if (dirtyCards.size > 0 || isTitleDirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    });
})();
