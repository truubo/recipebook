using Microsoft.AspNetCore.Mvc.Rendering;

namespace Recipebook.Models.ViewModels
{
    public class ListEditVm
    {
        // The entity you’re creating/editing
        public List List { get; set; } = new List();

        // Multi-select <option> list for recipes
        public List<SelectListItem> RecipeChoices { get; set; } = new();

        // The recipe IDs the user selected in the form
        public int[] SelectedRecipeIds { get; set; } = Array.Empty<int>();
    }
}
