using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Recipebook.Models;

namespace Recipebook.Models.ViewModels
{
    public class RecipeCreateEditVm
    {
        public string? UpdateButtonText { get; set; }

        public Recipe Recipe { get; set; } = new Recipe();

        public List<IngredientSelectViewModel> Ingredients { get; set; } = new List<IngredientSelectViewModel>();

        public int[] SelectedCategories { get; set; } = new int[0];
        public List<Direction> DirectionsList { get; set; } = new List<Direction>();

        public bool IsCopy { get; set; }
    }

    public class IngredientSelectViewModel
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        // ✅ changed from int to decimal + binder
        [ModelBinder(typeof(Recipebook.Infrastructure.Binding.FractionDecimalBinder))]
        public decimal Quantity { get; set; } = 1m;

        public Unit Unit { get; set; } = Unit.Piece;
    }
}