// wwwroot/js/categoriesHandler.js

// Modal instance for "Create new category"
const categoryModalElement = document.getElementById("createCategoryModal");
const categoryModal = categoryModalElement
    ? new bootstrap.Modal(categoryModalElement, {})
    : null;

// ==============================
// Helpers
// ==============================

// Update the button text based on selected options
function updateCategoriesDropdownText() {
    const select = document.getElementById("SelectedCategories");
    const buttonText = document.getElementById("categoriesDropdownText");
    if (!select || !buttonText) return;

    const selected = Array.from(select.options)
        .filter(o => o.selected)
        .map(o => o.text);

    if (selected.length === 0) {
        buttonText.innerText = "Select categories...";
    } else if (selected.length === 1) {
        buttonText.innerText = selected[0];
    } else {
        buttonText.innerText = `${selected[0]} +${selected.length - 1} more`;
    }
}

// Initialize once DOM is ready
document.addEventListener("DOMContentLoaded", () => {
    updateCategoriesDropdownText();
});

// ==============================
// Dropdown interaction
// ==============================

// Toggle one category on/off when a list item is clicked
function toggleCategoryFromList(event, element) {
    event.preventDefault();
    event.stopPropagation();

    const id = element.dataset.id;
    const select = document.getElementById("SelectedCategories");
    if (!select) return;

    const option = Array.from(select.options).find(o => o.value === id);
    if (!option) return;

    // Toggle selected state
    option.selected = !option.selected;

    // Toggle highlight on the visible item
    element.classList.toggle("active", option.selected);

    // Refresh button text
    updateCategoriesDropdownText();
}

// Client-side search filtering
function searchCategories(input) {
    const term = input.value.toLowerCase();
    const items = document.querySelectorAll("#categoriesList .category-option");

    items.forEach(a => {
        const name = a.dataset.name.toLowerCase();
        const li = a.closest("li");
        if (!li) return;
        li.style.display = name.includes(term) ? "" : "none";
    });
}

// ==============================
// Create-new-category flow
// ==============================

// Open the modal
function promptNewCategory() {
    if (!categoryModal) return;

    const nameInput = document.getElementById("categoryName");
    if (nameInput) {
        nameInput.value = "";
    }

    categoryModal.show();

    // Focus input
    setTimeout(() => {
        nameInput && nameInput.focus();
    }, 150);
}

// POST to /Categories/Create and update UI
async function createCategory() {
    const nameInput = document.querySelector("#createCategoryModal input#categoryName");
    if (!nameInput) return;

    const name = nameInput.value.trim();
    if (!name) return;

    // Mirror ingredient create: send FormData with Name
    const formData = new FormData();
    formData.append("Name", name);

    const response = await fetch("/Categories/Create", {
        method: "POST",
        body: formData,
        headers: { "Accept": "application/json" }
    });

    if (!response.ok) {
        alert("Error creating category.");
        return;
    }

    const data = await response.json(); // { id, name }

    // Update hidden select
    const select = document.getElementById("SelectedCategories");
    let option = Array.from(select.options).find(o => o.value === String(data.id));
    if (!option) {
        option = new Option(data.name, data.id, true, true);
        select.add(option);
    } else {
        option.selected = true;
    }

    // Add to visible dropdown list
    const list = document.getElementById("categoriesList");
    if (list) {
        const li = document.createElement("li");
        const a = document.createElement("a");
        a.href = "#";
        a.className = "dropdown-item category-option active";
        a.dataset.id = data.id;
        a.dataset.name = data.name;
        a.textContent = data.name;
        a.setAttribute("onclick", "toggleCategoryFromList(event, this)");
        li.appendChild(a);
        list.appendChild(li);
    }

    updateCategoriesDropdownText();

    // Close modal and reset
    nameInput.value = "";
    categoryModal.hide();
}
