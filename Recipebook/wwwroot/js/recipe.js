// ---------- Ingredient dropdown handling for Recipe create and edit page. ---------- //

let cachedIngredients;
let targetElement;
const ingredientModal = new bootstrap.Modal(document.getElementById("createIngredientModal"), {})

function promptNewIngredient(element) {
    console.log("summoning ingredient creation modal");
    targetElement = element.closest("div.dropdown");
    ingredientModal.show();
}

function setIngredient(id, name) {
    console.log(`setting ingredient with id ${id} and name ${name}`)
    targetElement.querySelector("button").innerText = name;
    targetElement.querySelector(".ingredientIdInput").value = id;
}

async function createIngredient() {
    const name = document.querySelector("#createIngredientModal").querySelector("input#ingredientName")
    let formData = new FormData();
    formData.append("Name", name.value)
    const createIngredientRequest = await window.fetch("/Ingredients/Create", {
        method: "POST",
        body: formData,
        headers: { "Accept": "application/json" }
    })
    if (!createIngredientRequest.ok) {
        throw new Error(`Error creating ingredient: ${createIngredientRequest.statusCode}`)
    }
    const result = await createIngredientRequest.json();
    console.log("seems like the ingredient was created!")
    cachedIngredients = null;
    setIngredient(result.id, result.name)
    name.value = ""
    ingredientModal.hide();
}

async function downloadIngredients() {
    const ingredientListResponse = await window.fetch("/Ingredients/All");

    if (!ingredientListResponse.ok) {
        // todo: add user friendly error
        throw new Error("Failed to retrieve ingredient list.")
    }
    console.log("successfully retrieved ingredients")
    // get json from API and set cachedIngredients
    const result = await ingredientListResponse.json();
    return result;
}

function selectIngredient(element) {
    console.log(`selectIngredient called from ${element}`)
    const ingredientId = element.dataset.id;
    targetElement = element.closest("div.dropdown");
    setIngredient(ingredientId, element.innerText);
}

function populateDropdown(element, ingredientsOverride) {
    console.log("populateDropdown called")
    let ingredients = ingredientsOverride != null ? ingredientsOverride : cachedIngredients;
    // get list of ingredients
    const ingredientListHtml = element.parentElement.querySelector("#ingredientsList");
    ingredientListHtml.innerHTML = "";
    ingredients.forEach(ingredient => {
        // create the elements for the dropdown
        var li = document.createElement("li")
        var a = document.createElement("a")
        a.className = "dropdown-item"
        a.textContent = ingredient.name;
        a.dataset.id = ingredient.id;
        a.setAttribute("onclick", "selectIngredient(this)")
        li.appendChild(a)
        ingredientListHtml.appendChild(li)
        console.log(`created li for ${ingredient.name}`)
    });
    console.log("finished creating list items")
    element.removeAttribute("onclick");
}

async function fetchIngredients(element) {
    console.log("fetchIngredients called")
    // if cachedIngredients already exists, populate dropdown
    if (cachedIngredients) {
        console.log("ingredients already fetched")
        populateDropdown(element);
        return;
    }

    const dropdownMenu = element.nextElementSibling;
    // fetch ingredients from server
    console.log("retrieving ingredients...")
    cachedIngredients = await downloadIngredients();
    // populate dropdown
    await populateDropdown(element);
    return;
}

function searchIngredients(element) {
    let newIngredients = [];
    cachedIngredients.forEach(ing => {
        if (ing.name.toLowerCase().includes(element.value.toLowerCase())) {
            newIngredients.push(ing);
        }
    })
    populateDropdown(element.parentElement, newIngredients)
}