using System.ComponentModel.DataAnnotations;

namespace Recipebook.Models
{
    public class Recipe
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Title { get; set; } = default!;

        [Required, MaxLength(2000)]
        public string Directions { get; set; } = default!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public bool Private { get; set; }

        // Foreign Key to User (author)
        public int AuthorId { get; set; }

        // Navigation for many-to-many
        public ICollection<IngredientRecipe> IngredientRecipes { get; set; } = new List<IngredientRecipe>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }

}
