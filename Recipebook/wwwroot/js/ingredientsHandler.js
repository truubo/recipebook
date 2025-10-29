const container = document.querySelector(".ingredients-container");
const template = document.querySelector(".ingredient-template").innerHTML;

function updateLabels() {
    const labelsExist = container.querySelector(".ingredient-labels");
    const hasIngredients = container.querySelectorAll(".ingredient-row").length > 0;

    if (hasIngredients && !labelsExist) {
        const labelRow = document.createElement("div");
        labelRow.className = "ingredient-labels row mb-2 fw-bold";
        labelRow.innerHTML = `<div class="col">Ingredient</div><div class="col">Quantity</div><div class="col">Unit</div>`;
        container.prepend(labelRow);
    } else if (!hasIngredients && labelsExist) {
        labelsExist.remove();
    }
}

// Event delegation for add/remove using classes only
document.body.addEventListener("click", e => {
    if (e.target.matches(".add-ingredient-btn")) {
        container.insertAdjacentHTML('beforeend', template.replace(/__index__/g, ingredientIndex++));
        updateLabels();
    }

    if (e.target.matches(".remove-ingredient-btn")) {
        e.target.closest(".ingredient-row").remove();
        updateLabels();
    }
});

// Initialize labels on page load
updateLabels();