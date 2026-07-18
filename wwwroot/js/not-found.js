(function () {
    const card = document.querySelector('[data-vocabulary-card]');
    const button = document.querySelector('[data-flip-card]');
    if (!card || !button) return;

    function setFlipped(flipped) {
        card.classList.toggle('is-flipped', flipped);
        card.setAttribute('aria-pressed', String(flipped));
        button.setAttribute('aria-pressed', String(flipped));
    }

    function flip() { setFlipped(!card.classList.contains('is-flipped')); }
    button.addEventListener('click', flip);
    card.addEventListener('keydown', (event) => {
        if (event.code === 'Space') {
            event.preventDefault();
            flip();
        }
    });
})();
