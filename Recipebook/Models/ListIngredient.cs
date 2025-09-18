using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class ListIngredient
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("Ingredient")]
        public int IngredientId { get; set; }
        
        public Ingredient Ingredient { get; set; }

        [Required]
        [Column(TypeName = "int")]
        [ForeignKey("List")]
        public int ListId { get; set; }
        public List List { get; set; }

        [Required]
        [Column(TypeName = "int")]
        public int Quantity { get; set; } = 1;
    }
}
