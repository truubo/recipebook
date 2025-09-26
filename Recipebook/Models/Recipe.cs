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


        [NotMapped]
         public string? OwnerEmail { get; set; }

        // Foreign Key to User (author)
        // has to be a string because of how MS identity works
        [Required]
        [Column(TypeName = "nvarchar(450)")]
        [ForeignKey("AspNetUsers")]
        public string AuthorId { get; set; }

        // author emails may change, so AuthorEmail will be set locally whenever needed
        [NotMapped]
        public string AuthorEmail { get; set; }

        // Navigation for many-to-many
        public ICollection<IngredientRecipe> IngredientRecipes { get; set; } = new List<IngredientRecipe>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public ICollection<ListRecipe> ListRecipes { get; set; } = new List<ListRecipe>();
    }

}
