using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class ListRecipe
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("Recipe")]
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("List")]
        public int ListId { get; set; }
        public List List { get; set; }
    }
}
