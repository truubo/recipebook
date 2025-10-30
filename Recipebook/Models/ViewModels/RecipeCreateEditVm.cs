using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Recipebook.Models;

namespace Recipebook.Models.ViewModels
{
    public class RecipeCreateEditVm
    {
        public string UpdateButtonText { get; set; }

        public Recipe Recipe { get; set; } = new Recipe();

        public List<IngredientSelectViewModel> Ingredients { get; set; } = new List<IngredientSelectViewModel>();

        public int[] SelectedCategories { get; set; } = new int[0];

        public bool IsCopy { get; set; }
    }

    public class IngredientSelectViewModel
    {
        [Required]
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        // Accept both decimals and fractions (as string input)
        [Required]
        [Display(Name = "Quantity")]
        public string QuantityText { get; set; } = "1";

        [Required]
        public Unit Unit { get; set; } = Unit.Piece;
    }
}