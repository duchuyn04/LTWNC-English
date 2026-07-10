(function () {
    const OVERLAY_ID = 'pt-overlay';
    const CLASS_VISIBLE = 'pt-overlay--visible';
    const CLASS_IN = 'pt-overlay--in';
    const CLASS_OUT = 'pt-overlay--out';

    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function getOverlay() {
        return document.getElementById(OVERLAY_ID);
    }

    function createOverlay() {
        let overlay = getOverlay();
        if (overlay) return overlay;
        overlay = document.createElement('div');
        overlay.id = OVERLAY_ID;
        overlay.className = CLASS_VISIBLE;
        document.body.appendChild(overlay);
        return overlay;
    }

    function fadeOut() {
        const overlay = getOverlay();
        if (!overlay) return;

        if (prefersReducedMotion()) {
            overlay.remove();
            return;
        }

        overlay.classList.add(CLASS_OUT);
        overlay.addEventListener('transitionend', () => overlay.remove(), { once: true });
        setTimeout(() => overlay.remove(), 500);
    }

    function fadeInAndNavigate(href) {
        const overlay = createOverlay();
        overlay.classList.remove(CLASS_VISIBLE);
        overlay.classList.remove(CLASS_OUT);

        if (prefersReducedMotion()) {
            location.href = href;
            return;
        }

        void overlay.offsetWidth;
        overlay.classList.add(CLASS_IN);
        setTimeout(() => {
            location.href = href;
        }, 350);
    }

    function shouldIntercept(anchor, event) {
        if (!anchor || !anchor.href) return false;
        if (anchor.hostname !== location.hostname) return false;
        if (event.button !== 0) return false;
        if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return false;
        if (anchor.target === '_blank' || anchor.hasAttribute('download')) return false;
        if (anchor.closest('[data-no-transition]')) return false;

        const href = anchor.getAttribute('href');
        if (!href || href.startsWith('#')) return false;
        if (href.startsWith('javascript:') || href.startsWith('mailto:') || href.startsWith('tel:')) return false;

        return true;
    }

    document.addEventListener('DOMContentLoaded', fadeOut);
    window.addEventListener('pageshow', fadeOut);

    document.addEventListener('click', (event) => {
        const anchor = event.target.closest('a[href]');
        if (!shouldIntercept(anchor, event)) return;
        event.preventDefault();
        fadeInAndNavigate(anchor.href);
    });
})();
