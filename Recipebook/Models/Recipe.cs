using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

        [Obsolete("This has been deprecated in favor of DirectionsList. Do not create new recipes with this property.")]
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
        // 🔧 Not persisted, not posted, not validated
        [NotMapped]
        [BindNever]
        [ValidateNever]
        public string? AuthorEmail { get; set; }   // <-- make nullable

        [Column(TypeName = "bit")]
        public bool IsArchived { get; set; } = false;

        [Display(Name = "Prep Time (minutes)")]
        [Range(0, 999, ErrorMessage = "Prep time must be between 0 and 999 minutes.")]
        public int? PrepTimeMinutes { get; set; }

        [Display(Name = "Cook Time (minutes)")]
        [Range(0, 999, ErrorMessage = "Cook time must be between 0 and 999 minutes.")]
        public int? CookTimeMinutes { get; set; }

        // Navigation for many-to-many
        public ICollection<IngredientRecipe> IngredientRecipes { get; set; } = new List<IngredientRecipe>();
        public ICollection<CategoryRecipe> CategoryRecipes { get; set; } = new List<CategoryRecipe>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public ICollection<ListRecipe> ListRecipes { get; set; } = new List<ListRecipe>();

        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

        public ICollection<Direction> DirectionsList { get; set; } = new List<Direction>();
    }

}
