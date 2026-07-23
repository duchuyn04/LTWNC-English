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
            showCardErrors(card, errors);
            return;
        }
        clearCardErrors(card);

        const currentSetId = await ensureSetCreated();
        if (!currentSetId) return;

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

    if (false) {
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
            li.textContent = `${r.frontText} → ${r.backText}`;
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

    }

    const btnImport = document.getElementById('btn-import');
    const importModal = document.getElementById('import-modal');
    const importFile = document.getElementById('import-file');
    const importFileMeta = document.getElementById('import-file-meta');
    const fileImportForm = document.getElementById('file-import-form');
    const fileImportPreview = document.getElementById('file-import-preview');
    const btnFilePreview = document.getElementById('btn-file-preview');
    const btnFileImportConfirm = document.getElementById('btn-file-import-confirm');
    const btnImportConfirm = document.getElementById('btn-import-confirm');
    const btnImportCancel = document.getElementById('btn-import-cancel');
    const btnImportClose = document.getElementById('btn-import-close');
    const importTabFile = document.getElementById('import-tab-file');
    const importTabPaste = document.getElementById('import-tab-paste');
    const importPanelFile = document.getElementById('import-panel-file');
    const importPanelPaste = document.getElementById('import-panel-paste');
    const importText = document.getElementById('import-text');
    const importDelimiter = document.getElementById('import-delimiter');
    const importPreview = document.getElementById('import-preview');
    const importModalStatus = document.getElementById('import-modal-status');
    const importModeInputs = Array.from(
        document.querySelectorAll('input[name="replaceAll"]'));

    let activeImportTab = 'file';
    let filePreviewValid = false;
    let lastImportTrigger = null;

    function setImportStatus(message, isError = false) {
        importModalStatus.textContent = message || '';
        importModalStatus.classList.toggle('is-error', isError);
    }

    function activateImportTab(tab) {
        activeImportTab = tab;
        const fileActive = tab === 'file';
        importTabFile.classList.toggle('is-active', fileActive);
        importTabPaste.classList.toggle('is-active', !fileActive);
        importTabFile.setAttribute('aria-selected', String(fileActive));
        importTabPaste.setAttribute('aria-selected', String(!fileActive));
        importPanelFile.hidden = !fileActive;
        importPanelPaste.hidden = fileActive;
        btnFileImportConfirm.hidden = !fileActive;
        btnImportConfirm.hidden = fileActive;
        if (!fileActive) {
            renderPastePreview(parseImportText(importText.value, importDelimiter.value));
        }
    }

    function resetFilePreview() {
        filePreviewValid = false;
        fileImportPreview.replaceChildren();
        btnFileImportConfirm.disabled = true;
        setImportStatus('');
    }

    function resetImportModal() {
        resetFilePreview();
        importFile.value = '';
        importFileMeta.textContent = '';
        importText.value = '';
        importPreview.replaceChildren();
        importModeInputs[0].checked = true;
        activateImportTab('file');
    }

    function openImportModal() {
        resetImportModal();
        importModal.style.display = 'flex';
        document.body.classList.add('modal-open');
        btnImportClose.focus();
    }

    function closeImportModal() {
        importModal.style.display = 'none';
        document.body.classList.remove('modal-open');
        resetImportModal();
        lastImportTrigger?.focus();
    }

    async function openImportAfterSavingIfNeeded() {
        lastImportTrigger = document.activeElement;
        if (isNewSet()) {
            const shouldSave = window.confirm(
                'Bộ thẻ chưa được lưu. Bạn có muốn lưu bộ thẻ trước khi nhập file không?');
            if (!shouldSave) return;
            const createdId = await ensureSetCreated();
            if (!createdId) return;
        }
        openImportModal();
    }

    function renderFilePreview(result) {
        fileImportPreview.replaceChildren();

        const summary = document.createElement('div');
        summary.className = 'import-preview-summary';
        const validLabel = document.createElement('span');
        validLabel.textContent = `Hợp lệ: ${result.validCount}`;
        const skippedLabel = document.createElement('span');
        skippedLabel.textContent = `Lỗi: ${result.skippedCount}`;
        summary.append(validLabel, skippedLabel);
        fileImportPreview.appendChild(summary);

        if (result.rows?.length) {
            const tableWrap = document.createElement('div');
            tableWrap.className = 'import-preview-table-wrap';
            const table = document.createElement('table');
            table.className = 'import-preview-table';
            const headers = [
                'Dòng', 'Thuật ngữ', 'Định nghĩa', 'IPA',
                'Loại từ', 'Ví dụ tiếng Anh', 'Nghĩa ví dụ tiếng Việt'
            ];
            const thead = document.createElement('thead');
            const headerRow = document.createElement('tr');
            headers.forEach(header => {
                const cell = document.createElement('th');
                cell.textContent = header;
                headerRow.appendChild(cell);
            });
            thead.appendChild(headerRow);
            const tbody = document.createElement('tbody');
            result.rows.forEach(row => {
                const previewRow = document.createElement('tr');
                [
                    row.rowNumber,
                    row.frontText,
                    row.backText,
                    row.pronunciation,
                    row.partOfSpeech,
                    row.exampleSentence,
                    row.exampleMeaning
                ].forEach(value => {
                    const cell = document.createElement('td');
                    cell.textContent = value ?? '';
                    previewRow.appendChild(cell);
                });
                tbody.appendChild(previewRow);
            });
            table.append(thead, tbody);
            tableWrap.appendChild(table);
            fileImportPreview.appendChild(tableWrap);
        }

        if (result.errors?.length) {
            const errorList = document.createElement('ul');
            errorList.className = 'import-error-list';
            result.errors.forEach(error => {
                const item = document.createElement('li');
                item.textContent = error.rowNumber > 0
                    ? `Dòng ${error.rowNumber}: ${error.reason}`
                    : error.reason;
                errorList.appendChild(item);
            });
            if (result.errorsOmittedCount > 0) {
                const omitted = document.createElement('li');
                omitted.textContent =
                    `Còn ${result.errorsOmittedCount} lỗi khác không hiển thị.`;
                errorList.appendChild(omitted);
            }
            fileImportPreview.appendChild(errorList);
        }
    }

    async function previewImportFile() {
        const file = importFile.files?.[0];
        const setId = getSetId();
        if (!file || !setId) return;

        btnFilePreview.disabled = true;
        setImportStatus('Đang kiểm tra file...');
        const formData = new FormData(fileImportForm);
        try {
            const response = await fetch(`/Set/${setId}/Import/Preview`, {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': antiforgeryToken,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(result.error || 'Không thể xem trước file.');
            }
            renderFilePreview(result);
            filePreviewValid = Number(result.validCount) > 0;
            btnFileImportConfirm.disabled = !filePreviewValid;
            setImportStatus(filePreviewValid
                ? 'File đã sẵn sàng để nhập.'
                : 'File chưa có dòng hợp lệ.');
        } catch (error) {
            resetFilePreview();
            setImportStatus(error.message || 'Không thể xem trước file.', true);
        } finally {
            btnFilePreview.disabled = !importFile.files?.length;
        }
    }

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

    function renderPastePreview(rows) {
        const valid = rows.filter(row => row.frontText && row.backText);
        const invalid = rows.filter(row => !row.frontText || !row.backText);
        importPreview.replaceChildren();
        const summary = document.createElement('p');
        summary.textContent = `Hợp lệ: ${valid.length}, lỗi: ${invalid.length}`;
        importPreview.appendChild(summary);
        const list = document.createElement('ul');
        valid.slice(0, 5).forEach(row => {
            const item = document.createElement('li');
            item.textContent = `${row.frontText} → ${row.backText}`;
            list.appendChild(item);
        });
        invalid.forEach(row => {
            const item = document.createElement('li');
            item.className = 'error';
            item.textContent = `Lỗi: "${row.frontText}" / "${row.backText}"`;
            list.appendChild(item);
        });
        importPreview.appendChild(list);
        btnImportConfirm.disabled = valid.length === 0;
    }

    async function submitPasteImport() {
        const rows = parseImportText(importText.value, importDelimiter.value)
            .filter(row => row.frontText && row.backText);
        if (!rows.length) return;
        const currentSetId = getSetId();
        if (!currentSetId) return;

        const replaceAll = document.getElementById('import-mode-replace').checked;
        if (replaceAll && !window.confirm(
            'Toàn bộ thẻ và tiến độ học hiện tại sẽ bị xóa. Bạn có chắc muốn ghi đè không?')) {
            return;
        }

        const existingCards = Array.from(container.querySelectorAll('.flashcard-card'));
        const payload = {
            setId: currentSetId,
            cards: rows.map(row => ({
                setId: currentSetId,
                frontText: row.frontText,
                backText: row.backText,
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
            if (!response.ok) throw new Error('Import thất bại.');
            const created = await response.json();
            if (replaceAll) {
                existingCards.forEach(card => card.remove());
            }
            created.forEach(cardData => {
                const card = createEmptyCard();
                card.dataset.id = cardData.id;
                card.querySelector('.input-front').value = cardData.frontText;
                card.querySelector('.input-back').value = cardData.backText;
                card.querySelector('.card-term').textContent = cardData.frontText;
                container.appendChild(card);
            });
            updateCardNumbering();
            setSaveStatus('Đã import', 'saved');
            closeImportModal();
        } catch (error) {
            setImportStatus(error.message || 'Import thất bại.', true);
            setSaveStatus('Lỗi import', 'error');
        }
    }

    btnImport.addEventListener('click', openImportAfterSavingIfNeeded);
    btnImportClose.addEventListener('click', closeImportModal);
    btnImportCancel.addEventListener('click', closeImportModal);
    importTabFile.addEventListener('click', () => activateImportTab('file'));
    importTabPaste.addEventListener('click', () => activateImportTab('paste'));
    btnFilePreview.addEventListener('click', previewImportFile);
    btnFileImportConfirm.addEventListener('click', () => {
        if (!filePreviewValid) return;
        const replaceAll = document.getElementById('import-mode-replace').checked;
        if (replaceAll && !window.confirm(
            'Toàn bộ thẻ và tiến độ học hiện tại sẽ bị xóa. Bạn có chắc muốn ghi đè không?')) {
            return;
        }
        fileImportForm.action =
            `/Set/${getSetId()}/Import`;
        fileImportForm.submit();
    });
    btnImportConfirm.addEventListener('click', submitPasteImport);
    importFile.addEventListener('change', () => {
        resetFilePreview();
        const file = importFile.files?.[0];
        importFileMeta.textContent = file
            ? `${file.name} · ${(file.size / 1024 / 1024).toFixed(2)} MB`
            : '';
        btnFilePreview.disabled = !file;
    });
    importModeInputs.forEach(input => {
        input.addEventListener('change', resetFilePreview);
    });
    [importText, importDelimiter].forEach(element => {
        element.addEventListener('input', () =>
            renderPastePreview(parseImportText(importText.value, importDelimiter.value)));
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && importModal.style.display !== 'none') {
            closeImportModal();
        }
    });
    importModal.addEventListener('click', event => {
        if (event.target === importModal) closeImportModal();
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
