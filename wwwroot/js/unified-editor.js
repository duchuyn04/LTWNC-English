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
            imageUrl: card.dataset.imageUrl || null,
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
        div.dataset.imageUrl = '';
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
    const importFile = document.getElementById('import-file');
    const importDropzone = importModal.querySelector('.import-file-dropzone');
    const importFileSelection = document.getElementById('import-file-selection');
    const importFileName = document.getElementById('import-file-name');
    const importReplace = document.getElementById('import-replace');
    const importFeedback = document.getElementById('import-feedback');
    const btnImportCancel = document.getElementById('btn-import-cancel');
    const btnImportConfirm = document.getElementById('btn-import-confirm');
    const maxImportBytes = 10 * 1024 * 1024;
    const allowedImportExtensions = ['.csv', '.xlsx'];
    let selectedImportFile = null;
    let importCardToReveal = null;
    let importIsBusy = false;

    function formatFileSize(bytes) {
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${Math.ceil(bytes / 1024)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    function importFileValidationError(file) {
        if (!file) return 'Vui lòng chọn file cần nhập.';
        const fileName = file.name.toLowerCase();
        if (!allowedImportExtensions.some(extension => fileName.endsWith(extension))) {
            return 'Chỉ hỗ trợ file CSV hoặc XLSX.';
        }
        if (file.size === 0) return 'File đang trống.';
        if (file.size > maxImportBytes) return 'File không được vượt quá 10 MB.';
        return '';
    }

    function showImportFeedback(message, type, errors, omittedErrorCount) {
        importFeedback.replaceChildren();
        importFeedback.className = `import-feedback is-${type || 'error'}`;
        importFeedback.hidden = false;

        const summary = document.createElement('p');
        summary.textContent = message;
        importFeedback.appendChild(summary);

        if (Array.isArray(errors) && errors.length > 0) {
            const list = document.createElement('ul');
            errors.forEach(error => {
                const item = document.createElement('li');
                const rowNumber = error.rowNumber ?? error.RowNumber;
                const reason = error.reason ?? error.Reason ?? 'Dữ liệu không hợp lệ.';
                item.textContent = rowNumber > 0 ? `Dòng ${rowNumber}: ${reason}` : reason;
                list.appendChild(item);
            });
            if (omittedErrorCount > 0) {
                const omitted = document.createElement('li');
                omitted.textContent = `Còn ${omittedErrorCount} lỗi khác không hiển thị.`;
                list.appendChild(omitted);
            }
            importFeedback.appendChild(list);
        }
    }

    function clearImportFeedback() {
        importFeedback.replaceChildren();
        importFeedback.className = 'import-feedback';
        importFeedback.hidden = true;
    }

    function setImportBusy(isBusy) {
        importIsBusy = isBusy;
        importFile.disabled = isBusy;
        importReplace.disabled = isBusy;
        btnImportCancel.disabled = isBusy;
        btnImportConfirm.disabled = isBusy || !selectedImportFile;
        btnImportConfirm.textContent = isBusy ? 'Đang nhập…' : 'Nhập file';
    }

    function selectImportFile(file) {
        selectedImportFile = null;
        importFileSelection.hidden = !file;
        importFileName.textContent = file ? `${file.name} · ${formatFileSize(file.size)}` : '';

        const validationError = importFileValidationError(file);
        if (validationError) {
            btnImportConfirm.disabled = true;
            showImportFeedback(validationError, 'error');
            return;
        }

        selectedImportFile = file;
        clearImportFeedback();
        btnImportConfirm.disabled = false;
    }

    function resetImportDialog() {
        selectedImportFile = null;
        importCardToReveal = null;
        importFile.value = '';
        importFileSelection.hidden = true;
        importFileName.textContent = '';
        importReplace.checked = false;
        btnImportCancel.textContent = 'Hủy';
        clearImportFeedback();
        setImportBusy(false);
    }

    function closeImportDialog() {
        if (importIsBusy) return;
        importModal.style.display = 'none';
        importDropzone.classList.remove('is-dragging');
        btnImport.focus();

        if (importCardToReveal) {
            const card = importCardToReveal;
            importCardToReveal = null;
            requestAnimationFrame(() => card.scrollIntoView({ behavior: 'smooth', block: 'center' }));
        }
    }

    function removeEditorCard(card) {
        const id = card.dataset.id;
        if (pendingSaves.has(id)) {
            clearTimeout(pendingSaves.get(id));
            pendingSaves.delete(id);
        }
        dirtyCards.delete(id);
        card.remove();
    }

    function isPristineLocalCard(card) {
        return card.dataset.id.startsWith('new-')
            && Array.from(card.querySelectorAll('input, textarea'))
                .every(input => !input.value.trim());
    }

    function appendImportedCard(data) {
        const card = createEmptyCard();
        card.dataset.id = String(data.id);
        card.dataset.order = String(data.orderIndex ?? '');
        card.dataset.starred = data.isStarred ? 'true' : 'false';
        card.dataset.imageUrl = data.imageUrl || '';
        card.querySelector('.input-front').value = data.frontText || '';
        card.querySelector('.input-back').value = data.backText || '';
        card.querySelector('.input-pronunciation').value = data.pronunciation || '';
        card.querySelector('.input-part-of-speech').value = data.partOfSpeech || '';
        card.querySelector('.input-example-sentence').value = data.exampleSentence || '';
        card.querySelector('.input-example-meaning').value = data.exampleMeaning || '';
        card.querySelector('.input-synonyms').value = data.synonyms || '';
        card.querySelector('.card-term').textContent = data.frontText || '';
        card.querySelector('.btn-star').textContent = data.isStarred ? '★' : '☆';
        card.classList.remove('expanded');
        card.classList.add('collapsed');
        card.querySelector('.btn-toggle').textContent = '▼';
        container.appendChild(card);
        return card;
    }

    async function readImportResponse(response) {
        const responseText = await response.text();
        if (!responseText) return {};
        try {
            return JSON.parse(responseText);
        } catch {
            return { message: responseText };
        }
    }

    btnImport.addEventListener('click', () => {
        resetImportDialog();
        importModal.style.display = 'flex';
        requestAnimationFrame(() => importFile.focus());
    });

    btnImportCancel.addEventListener('click', closeImportDialog);

    importFile.addEventListener('change', () => {
        selectImportFile(importFile.files?.[0] || null);
    });

    ['dragenter', 'dragover'].forEach(eventName => {
        importDropzone.addEventListener(eventName, event => {
            event.preventDefault();
            if (!importIsBusy) importDropzone.classList.add('is-dragging');
        });
    });

    ['dragleave', 'dragend'].forEach(eventName => {
        importDropzone.addEventListener(eventName, () => {
            importDropzone.classList.remove('is-dragging');
        });
    });

    importDropzone.addEventListener('drop', event => {
        event.preventDefault();
        importDropzone.classList.remove('is-dragging');
        if (!importIsBusy) selectImportFile(event.dataTransfer?.files?.[0] || null);
    });

    importModal.addEventListener('click', event => {
        if (event.target === importModal) closeImportDialog();
    });

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && importModal.style.display !== 'none') {
            closeImportDialog();
        }
    });

    btnImportConfirm.addEventListener('click', async () => {
        const validationError = importFileValidationError(selectedImportFile);
        if (validationError) {
            showImportFeedback(validationError, 'error');
            return;
        }

        const currentSetId = getSetId() || await ensureSetCreated();
        if (!currentSetId) {
            showImportFeedback('Hãy nhập tên bộ từ trước khi tải file lên.', 'error');
            return;
        }

        const replaceAll = importReplace.checked;
        const existingCards = Array.from(container.querySelectorAll('.flashcard-card'));
        if (replaceAll) {
            const cardsBeingSaved = existingCards
                .filter(card => dirtyCards.has(card.dataset.id) || card.savePromise)
                .map(card => saveCard(card));
            await Promise.all(cardsBeingSaved);
        }

        const formData = new FormData();
        formData.append('file', selectedImportFile, selectedImportFile.name);
        formData.append('replaceAll', replaceAll.toString());

        setImportBusy(true);
        setSaveStatus('Đang nhập file...', 'saving');
        try {
            const response = await apiFetch(`/Set/${currentSetId}/ImportFile`, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: formData
            });
            const result = await readImportResponse(response);

            if (!response.ok) {
                showImportFeedback(
                    result.message || 'Không thể nhập file. Vui lòng thử lại.',
                    'error',
                    result.errors,
                    result.omittedErrorCount);
                setSaveStatus('Lỗi nhập file', 'error');
                return;
            }

            if (replaceAll) {
                existingCards.forEach(removeEditorCard);
            } else {
                existingCards.filter(isPristineLocalCard).forEach(removeEditorCard);
            }

            const createdCards = Array.isArray(result.cards) ? result.cards : [];
            let firstImportedCard = null;
            createdCards.forEach(data => {
                const importedCard = appendImportedCard(data);
                firstImportedCard ??= importedCard;
            });

            updateCardNumbering();
            syncFinishButtons();
            selectedImportFile = null;
            importFile.value = '';
            importFileSelection.hidden = true;
            importFileName.textContent = '';
            importCardToReveal = firstImportedCard;

            const importedCount = result.importedCount ?? createdCards.length;
            const skippedCount = result.skippedCount ?? 0;
            setSaveStatus(`Đã nhập ${importedCount} thẻ`, 'saved');

            if (skippedCount > 0) {
                btnImportCancel.textContent = 'Đóng';
                showImportFeedback(
                    `Đã nhập ${importedCount} thẻ. Có ${skippedCount} dòng bị bỏ qua.`,
                    'warning',
                    result.errors,
                    result.omittedErrorCount);
            } else {
                setImportBusy(false);
                closeImportDialog();
            }
        } catch (err) {
            showImportFeedback('Mất kết nối khi tải file. Vui lòng thử lại.', 'error');
            setSaveStatus('Lỗi nhập file', 'error');
            console.error('File import failed:', err);
        } finally {
            setImportBusy(false);
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
