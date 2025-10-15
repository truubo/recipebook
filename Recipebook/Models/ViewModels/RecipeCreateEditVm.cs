using System.Collections.Generic;
using Recipebook.Models;

namespace Recipebook.Models.ViewModels
{
    public class RecipeCreateEditVm
    {
        public Recipe Recipe { get; set; } = new Recipe();

        public List<IngredientSelectViewModel> Ingredients { get; set; } = new List<IngredientSelectViewModel>();

        public int[] SelectedCategories { get; set; } = new int[0];

        public bool IsCopy { get; set; }
    }

    public class IngredientSelectViewModel
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;

        public Unit Unit { get; set; } = Unit.Piece;
    }
}