using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class CategoryRecipe
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }
    }
}