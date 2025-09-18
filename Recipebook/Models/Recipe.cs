using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    public class Recipe
    {
        [Required]
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Title { get; set; } = default!;

        [Required, MaxLength(2000)]
        [Column(TypeName = "varchar(2000)")]
        public string Directions { get; set; } = default!;

        [MaxLength(2000)]
        [Column(TypeName = "varchar(2000)")]
        public string? Description { get; set; }

        [Column(TypeName = "bit")]
        public bool Private { get; set; } = false;

        // Foreign Key to User (author)
        [Required]
        [Column(TypeName = "int")]
        //[ForeignKey("User")]
        public int AuthorId { get; set; }

        // Navigation for many-to-many
        public ICollection<IngredientRecipe> IngredientRecipes { get; set; } = new List<IngredientRecipe>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }

}
