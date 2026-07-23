(() => {
    const variants = ["A", "B", "C"];
    const host = document.querySelector("[data-study-mode-prototype]");
    const switcher = document.querySelector("[data-prototype-switcher]");

    if (!host || !switcher) {
        return;
    }

    const current = variants.includes(host.dataset.variant)
        ? host.dataset.variant
        : "A";

    const navigate = (offset) => {
        const currentIndex = variants.indexOf(current);
        const nextIndex = (currentIndex + offset + variants.length) % variants.length;
        const url = new URL(window.location.href);
        url.searchParams.set("variant", variants[nextIndex]);
        window.location.assign(url.toString());
    };

    switcher.querySelectorAll("[data-prototype-direction]").forEach((button) => {
        button.addEventListener("click", () => {
            navigate(Number(button.dataset.prototypeDirection));
        });
    });

    switcher.addEventListener("keydown", (event) => {
        if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        navigate(event.key === "ArrowLeft" ? -1 : 1);
    });

    const launcher = document.querySelector("[data-mode-launcher]");
    const menu = document.querySelector("[data-mode-menu]");

    if (!launcher || !menu) {
        return;
    }

    const setMenuOpen = (open) => {
        launcher.setAttribute("aria-expanded", String(open));
        menu.hidden = !open;
    };

    launcher.addEventListener("click", () => {
        setMenuOpen(launcher.getAttribute("aria-expanded") !== "true");
    });

    document.addEventListener("click", (event) => {
        if (!launcher.contains(event.target) && !menu.contains(event.target)) {
            setMenuOpen(false);
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !menu.hidden) {
            setMenuOpen(false);
            launcher.focus();
        }
    });
})();
