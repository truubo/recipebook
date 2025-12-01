// /wwwroot/js/listsHandler.js

/* ------------------ LISTS DROPDOWN ------------------ */
function getSelectedListsSelect() {
    return document.getElementById("SelectedLists");
}

function getActiveListItems() {
    return document.querySelectorAll("#listsList .list-option.active");
}

// Sync hidden <select> with visible active items
function syncListsSelection() {
    const select = getSelectedListsSelect();
    if (!select) return;

    const activeItems = getActiveListItems();
    Array.from(select.options).forEach(o => (o.selected = false));

    activeItems.forEach(item => {
        const id = item.dataset.id;
        const name = item.dataset.name;

        let opt = Array.from(select.options).find(o => o.value === id);
        if (!opt) {
            opt = new Option(name, id, true, true);
            select.add(opt);
        } else {
            opt.selected = true;
        }
    });

    updateListsDropdownText();
}

function updateListsDropdownText() {
    const span = document.getElementById("listsDropdownText");
    if (!span) return;

    const active = Array.from(getActiveListItems());
    const count = active.length;

    if (count === 0) {
        span.textContent = "Select lists...";
        return;
    }

    const firstName = active[0].dataset.name;
    span.textContent = count === 1 ? firstName : `${firstName} + ${count - 1} more`;
}

function toggleListFromList(evt, el) {
    evt.preventDefault();
    evt.stopPropagation();

    el.classList.toggle("active");
    syncListsSelection();
}

function searchLists(input) {
    const term = (input.value || "").toLowerCase();
    const options = document.querySelectorAll("#listsList .list-option");

    options.forEach(opt => {
        const name = opt.dataset.name.toLowerCase();
        const li = opt.closest("li");
        if (!li) return;
        li.style.display = name.includes(term) ? "" : "none";
    });
}

/* ------------------ RECIPES DROPDOWN ------------------ */
window.toggleListRecipe = function (event, element) {
    event.preventDefault();
    event.stopPropagation();

    const id = element.dataset.id;
    const hiddenSelect = document.getElementById("SelectedRecipeIds");
    if (!hiddenSelect) return;

    element.classList.toggle("active");

    const option = [...hiddenSelect.options].find(o => o.value === id);
    if (option) option.selected = !option.selected;

    updateListRecipesDropdownText();
};

window.updateListRecipesDropdownText = function () {
    const hiddenSelect = document.getElementById("SelectedRecipeIds");
    if (!hiddenSelect) return;

    const selectedNames = [...hiddenSelect.selectedOptions].map(o => o.text);
    const span = document.getElementById("listsRecipesDropdownText");
    if (!span) return;

    if (selectedNames.length === 0) {
        span.textContent = "Select recipes...";
        return;
    }

    if (selectedNames.length > 2) {
        const firstTwo = selectedNames.slice(0, 2).join(", ");
        const remaining = selectedNames.length - 2;
        span.textContent = `${firstTwo} +${remaining} more`;
    } else {
        span.textContent = selectedNames.join(", ");
    }
};

window.searchListRecipes = function (input) {
    const filter = input.value.toLowerCase();
    const items = document.querySelectorAll("#listsRecipesList .list-recipe-option");

    items.forEach(item => {
        const name = item.dataset.name.toLowerCase();
        item.parentElement.style.display = name.includes(filter) ? "" : "none";
    });
};

/* ------------------ INIT ON PAGE LOAD ------------------ */
document.addEventListener("DOMContentLoaded", () => {
    // Lists dropdown
    if (getSelectedListsSelect()) {
        syncListsSelection();
    }

    // Recipes dropdown
    if (document.getElementById("SelectedRecipeIds")) {
        updateListRecipesDropdownText();
        const dropdownMenu = document.querySelector("#listsRecipesDropdown .dropdown-menu");
        if (dropdownMenu) {
            dropdownMenu.addEventListener("click", e => e.stopPropagation());
        }
    }
});