(function () {
    const cropper = document.querySelector('[data-avatar-cropper]');
    if (!cropper) return;

    const input = document.querySelector('#avatar');
    const canvas = cropper.querySelector('[data-avatar-canvas]');
    const zoomInput = cropper.querySelector('[data-avatar-zoom]');
    const form = input?.form;
    const context = canvas?.getContext('2d');
    let image = null;
    let zoom = 1;
    let offsetX = 0;
    let offsetY = 0;
    let dragging = false;
    let startX = 0;
    let startY = 0;

    function draw() {
        if (!context || !canvas) return;
        context.clearRect(0, 0, canvas.width, canvas.height);
        if (!image) return;

        const scale = Math.max(canvas.width / image.width, canvas.height / image.height) * zoom;
        const width = image.width * scale;
        const height = image.height * scale;
        context.drawImage(image, (canvas.width - width) / 2 + offsetX, (canvas.height - height) / 2 + offsetY, width, height);
    }

    function loadImage(source, revokeUrl = false) {
        const nextImage = new Image();
        nextImage.onload = () => {
            image = nextImage;
            zoom = 1;
            offsetX = 0;
            offsetY = 0;
            if (zoomInput) zoomInput.value = '1';
            draw();
            if (revokeUrl) URL.revokeObjectURL(source);
        };
        nextImage.onerror = () => {
            if (revokeUrl) URL.revokeObjectURL(source);
        };
        nextImage.src = source;
    }

    input?.addEventListener('change', () => {
        const file = input.files?.[0];
        if (!file) return;
        loadImage(URL.createObjectURL(file), true);
    });

    zoomInput?.addEventListener('input', () => { zoom = Number(zoomInput.value); draw(); });
    canvas?.addEventListener('pointerdown', (event) => {
        dragging = true;
        startX = event.clientX - offsetX;
        startY = event.clientY - offsetY;
        canvas.setPointerCapture(event.pointerId);
    });
    canvas?.addEventListener('pointermove', (event) => {
        if (!dragging) return;
        offsetX = event.clientX - startX;
        offsetY = event.clientY - startY;
        draw();
    });
    canvas?.addEventListener('pointerup', () => { dragging = false; });
    canvas?.addEventListener('keydown', (event) => {
        const step = event.shiftKey ? 10 : 2;
        if (event.key === 'ArrowLeft') offsetX -= step;
        else if (event.key === 'ArrowRight') offsetX += step;
        else if (event.key === 'ArrowUp') offsetY -= step;
        else if (event.key === 'ArrowDown') offsetY += step;
        else return;
        event.preventDefault();
        draw();
    });

    form?.addEventListener('submit', (event) => {
        if (!image || !canvas) return;
        event.preventDefault();
        canvas.toBlob((blob) => {
            if (!blob) return;
            const dataTransfer = new DataTransfer();
            dataTransfer.items.add(new File([blob], 'avatar.png', { type: 'image/png' }));
            input.files = dataTransfer.files;
            form.submit();
        }, 'image/png');
    });

    const avatarSource = cropper.dataset.avatarSource;
    if (avatarSource) loadImage(avatarSource);
    else draw();
})();
