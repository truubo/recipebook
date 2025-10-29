const directionsContainer = document.querySelector(".direction-container");
const directionsTemplate = document.querySelector(".direction-template").innerHTML;

function addDirection() {
    directionsContainer.insertAdjacentHTML('beforeend', directionsTemplate.replace(/__index__/g, directionIndex).replace(/__visibleIndex__/g, directionIndex + 1));
    directionIndex++;
}

function removeDirection(e) {
    e.closest(".direction").remove();
    updateDirections();
}

function updateDirections() {
    const directions = directionsContainer.querySelectorAll(".direction")
    directions.forEach((direction, i) => {
        const stepNumber = direction.querySelector(".step-number");
        if (stepNumber) stepNumber.textContent = i + 1;
        const input = direction.querySelector("input");
        input.name = `Recipe.DirectionsList[${i}].StepNumber`;
        input.value = i + 1
        direction.querySelector("textarea").name = `Recipe.DirectionsList[${i}].StepDescription`
    })
    directionIndex = directions.length;
}