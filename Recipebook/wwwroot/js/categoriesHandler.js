// /wwwroot/js/categoriesHandler.js
function getSelectedCategoriesSelect() {
    return document.getElementById("SelectedCategories");
}

function getActiveCategoryItems() {
    return document.querySelectorAll("#categoriesList .category-option.active");
}

// Update hidden <select> based on active list items
function syncCategoriesSelection() {
    const select = getSelectedCategoriesSelect();
    if (!select) return;

    const activeItems = getActiveCategoryItems();

    // Clear all selected flags
    Array.from(select.options).forEach(o => (o.selected = false));

    // Mark/append options that correspond to active items
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

    updateCategoriesDropdownText();
}

// Set the button text summary
function updateCategoriesDropdownText() {
    const span = document.getElementById("categoriesDropdownText");
    if (!span) return;

    const active = Array.from(getActiveCategoryItems());
    const count = active.length;

    if (count === 0) {
        span.textContent = "Select categories...";
        return;
    }

    // Show "FirstName + N more" when multiple selected
    const firstName = active[0].dataset.name;
    if (count === 1) {
        span.textContent = firstName;
    } else {
        const remaining = count - 1; // this is the "5 more" part, etc.
        span.textContent = `${firstName} + ${remaining} more`;
    }
}

// ----- event handlers used by Razor -----

// Called from onclick="toggleCategoryFromList(event, this)"
function toggleCategoryFromList(evt, el) {
    // Prevent page jump / navigation and keep dropdown from closing
    evt.preventDefault();
    evt.stopPropagation();

    el.classList.toggle("active");
    syncCategoriesSelection();
}

// Called from oninput="searchCategories(this)"
function searchCategories(input) {
    const term = (input.value || "").toLowerCase();
    const options = document.querySelectorAll("#categoriesList .category-option");

    options.forEach(opt => {
        const name = opt.dataset.name.toLowerCase();
        const li = opt.closest("li");
        if (!li) return;

        li.style.display = name.includes(term) ? "" : "none";
    });
}

// Called from onclick="promptNewCategory()"
function promptNewCategory() {
    const modalEl = document.getElementById("createCategoryModal");
    if (!modalEl) return;

    const nameInput = document.getElementById("categoryName");
    if (nameInput) nameInput.value = "";

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
}

// Called from onclick="createCategory()" in the modal
async function createCategory() {
    const modalEl = document.getElementById("createCategoryModal");
    if (!modalEl) return;

    const nameInput = document.getElementById("categoryName");
    const spinner = modalEl.querySelector(".spinner-border");

    const name = (nameInput.value || "").trim();
    if (!name) {
        nameInput.focus();
        return;
    }

    spinner.style.display = "inline-block";

    try {
        const resp = await fetch("/Categories/CreateFromRecipe", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ name })
        });

        if (!resp.ok) {
            console.error("CreateFromRecipe failed, status:", resp.status);
            throw new Error("Failed to create category");
        }

        // Expect { id: 123, name: "Foo" }
        const data = await resp.json();

        const list = document.getElementById("categoriesList");
        const li = document.createElement("li");
        li.innerHTML = `
            <a href="#"
               class="dropdown-item category-option active"
               data-id="${data.id}"
               data-name="${data.name}"
               onclick="toggleCategoryFromList(event, this)">
                ${data.name}
            </a>
        `;
        list.appendChild(li);

        // Sync selection + preview text
        syncCategoriesSelection();

        const modal = bootstrap.Modal.getInstance(modalEl);
        modal.hide();
    } catch (err) {
        console.error(err);
        alert("Error creating category. Please try again.");
    } finally {
        spinner.style.display = "none";
    }
}

// On page load, initialize the dropdown text & hidden select (for Edit page)
document.addEventListener("DOMContentLoaded", () => {
    syncCategoriesSelection();
});
