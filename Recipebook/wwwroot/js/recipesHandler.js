// Reusable alert (bottom-right, fades out after 2.5s)
let activeAlert = null;

function showAlert(message, type = "brown") {
    if (activeAlert) {
        activeAlert.remove();
        activeAlert = null;
    }

    const alert = document.createElement("div");
    alert.className = `alert alert-${type} fade show position-fixed bottom-0 end-0 m-3 shadow`;
    alert.style.zIndex = "1050";
    alert.style.transition = "opacity 0.5s ease";
    alert.textContent = message;

    document.body.appendChild(alert);
    activeAlert = alert;

    setTimeout(() => {
        alert.style.opacity = "0";
        setTimeout(() => {
            alert.remove();
            activeAlert = null;
        }, 500);
    }, 2500);
}

// Determine if we're on the Recipes index page
const isRecipeIndex = window.location.pathname.toLowerCase().includes("/recipes");

// FAVORITES TOGGLE HANDLER
document.addEventListener('submit', async (e) => {
    const form = e.target;
    if (!form.matches('form[action*="Favorites/Toggle"]')) return;
    e.preventDefault();

    const icon = form.querySelector('i');
    if (!icon) return;

    const recipeName = form.dataset.recipeName || "Recipe";
    const wasFav = icon.classList.contains('bi-star-fill');

    // Optimistic UI feedback
    icon.classList.toggle('bi-star');
    icon.classList.toggle('bi-star-fill');
    icon.classList.toggle('unchecked');

    try {
        const res = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!res.ok) throw new Error();

        showAlert(wasFav
            ? `Removed '${recipeName}' from favorites.`
            : `Added '${recipeName}' to favorites!`);

        // Fade out card if removing from favorites tab
        if (isRecipeIndex && window.location.search.includes("scope=favorites") && wasFav) {
            const card = form.closest('.recipe-card');
            if (card) {
                card.style.transition = "opacity 0.5s ease";
                card.style.opacity = "0";
                setTimeout(() => card.remove(), 500);
            }
        }

    } catch {
        icon.classList.toggle('bi-star');
        icon.classList.toggle('bi-star-fill');
        icon.classList.toggle('unchecked');
        showAlert(`Something went wrong with '${recipeName}'. Please try again.`, "danger");
    }
});

// LIKE/DISLIKE TOGGLE HANDLER
document.addEventListener('submit', async (e) => {
    const form = e.target;
    if (!form.matches('form[action*="Votes/ToggleLike"], form[action*="Votes/ToggleDislike"]')) return;
    e.preventDefault();

    try {
        const res = await fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!res.ok) throw new Error();

        // Reload page to reflect updated counts
        window.location.reload();
    } catch {
        alert("Vote failed — please try again.");
    }
});

/* ============================================================
   DESCRIPTION COLLAPSE / EXPAND WITH SMOOTH HEIGHT ANIMATION
   ============================================================ */
document.addEventListener('DOMContentLoaded', () => {
    const toggle = document.querySelector('.desc-toggle');
    const content = document.querySelector('.desc-content');
    const arrow = toggle?.querySelector('.toggle-arrow');

    if (!toggle || !content) return;

    // initial: expanded
    content.style.height = 'auto';
    content.style.opacity = '1';
    content.classList.remove('closed');

    function openContent() {
        content.classList.remove('closed', 'closing');
        content.style.height = '0px';
        void content.offsetHeight; // reflow
        content.style.transition = 'height 350ms ease, opacity 200ms ease';
        content.style.height = content.scrollHeight + 'px';
        content.style.opacity = '1';

        content.addEventListener('transitionend', function handler(e) {
            if (e.propertyName === 'height') {
                content.style.height = 'auto';
                content.removeEventListener('transitionend', handler);
            }
        });

        toggle.setAttribute('aria-expanded', 'true');
        content.setAttribute('aria-hidden', 'false');
        if (arrow) arrow.style.transform = 'rotate(0deg)';
    }

    function closeContent() {
        content.style.height = content.scrollHeight + 'px';
        void content.offsetHeight;
        requestAnimationFrame(() => {
            content.style.transition = 'height 350ms ease, opacity 200ms ease';
            content.style.height = '0px';
            content.style.opacity = '0';
            content.classList.add('closing');
        });

        content.addEventListener('transitionend', function handler(e) {
            if (e.propertyName === 'height') {
                content.classList.add('closed');
                content.classList.remove('closing');
                content.style.height = '0px';
                content.removeEventListener('transitionend', handler);
            }
        });

        toggle.setAttribute('aria-expanded', 'false');
        content.setAttribute('aria-hidden', 'true');
        if (arrow) arrow.style.transform = 'rotate(180deg)';
    }

    let isOpen = true;
    toggle.addEventListener('click', () => {
        if (isOpen) closeContent();
        else openContent();
        isOpen = !isOpen;
    });

    toggle.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            toggle.click();
        }
    });
});