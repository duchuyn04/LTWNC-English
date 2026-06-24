// Flashcard flip
function toggleFlip() {
    document.querySelector('.flashcard')?.classList.toggle('flipped');
}

// Add stagger animation to list items
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.stagger-item').forEach((el, i) => {
        el.style.animationDelay = `${i * 80}ms`;
    });
});
