const directionsContainer = document.querySelector(".direction-container");
const directionsTemplate = document.querySelector(".direction-template").innerHTML;

// create new direction element
function addDirection() {
    directionsContainer.insertAdjacentHTML('beforeend', directionsTemplate.replace(/__index__/g, directionIndex).replace(/__visibleIndex__/g, directionIndex + 1));
    directionIndex++;
}

// remove direction element where e is the delete button for the target direction
function removeDirection(e) {
    e.closest(".direction").remove();
    updateDirections();
}

// update direction numbers and input names
function updateDirections() {
    const directions = directionsContainer.querySelectorAll(".direction")
    // go through each direction and update step numbers and input names
    directions.forEach((direction, i) => {
        const stepNumber = direction.querySelector(".step-number");
        if (stepNumber) stepNumber.textContent = i + 1;
        const input = direction.querySelector("input");
        input.name = `Recipe.DirectionsList[${i}].StepNumber`;
        input.value = i + 1
        direction.querySelector("textarea").name = `Recipe.DirectionsList[${i}].StepDescription`
    })
    // update directionIndex to ensure addDirection uses the correct index
    directionIndex = directions.length;
}