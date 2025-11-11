using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public enum Unit
    {
        Piece,
        Gram,
        Kilogram,
        Milliliter,
        Liter,
        Cup,
        Tablespoon,
        Teaspoon,
        Unit,
        Ounce,
        Pound
    }

    public class IngredientRecipe
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }
        public Ingredient? Ingredient { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        // ✅ changed: now supports fractions/decimals
        [Required]
        [ModelBinder(typeof(Recipebook.Infrastructure.Binding.FractionDecimalBinder))]
        [Column(TypeName = "decimal(10,4)")]
        public decimal Quantity { get; set; } = 1m;

        [Required, MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public Unit Unit { get; set; }
    }
}
