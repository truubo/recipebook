using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class Category
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }
        [Required]
        [Column(TypeName = "nvarchar(50)"), MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

        public ICollection<CategoryRecipe> CategoryRecipes { get; set; } = new List<CategoryRecipe>();

        // NEW: who owns this category
        public string OwnerId { get; set; } = string.Empty;
    }
}