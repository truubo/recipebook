using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Recipebook.Models
{
    public class Ingredient
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? Name { get; set; }

        public ICollection<IngredientRecipe>? IngredientRecipes { get; set; }

        public string OwnerId { get; set; } = string.Empty;

    }
}
