(() => {
    document.querySelectorAll("[data-password-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            const inputId = button.getAttribute("aria-controls");
            const input = inputId ? document.getElementById(inputId) : null;

            if (!(input instanceof HTMLInputElement)) return;

            const reveal = input.type === "password";
            input.type = reveal ? "text" : "password";
            button.textContent = reveal ? "Ẩn" : "Hiện";
            button.setAttribute("aria-label", reveal ? "Ẩn mật khẩu" : "Hiện mật khẩu");
        });
    });
})();
