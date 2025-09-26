// Models/ListRecipe.cs (unchanged properties)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class ListRecipe
    {
        [Required]
        public int Id { get; set; }

        [Required, Column(TypeName = "int")]
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = default!;

        [Required, Column(TypeName = "int")]
        public int ListId { get; set; }
        public List List { get; set; } = default!;
    }
}
