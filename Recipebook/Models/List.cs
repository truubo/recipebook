using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    /// <summary>
    /// Represents a user-owned list (e.g., grocery list, recipe plan).
    /// Uses the ListType enum to distinguish what kind of list this is.
    /// </summary>
    public class List
    {
        /// <summary>
        /// Primary key for the List table.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Display name of the list.
        /// Limited to 20 characters.
        /// </summary>
        [Required, MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Name { get; set; } = default!;

        /// <summary>
        /// Enum value representing the type of list (Recipes, Ingredients, etc.).
        /// Stored as an int in the database by default.
        /// </summary>
        [Required]
        public ListType ListType { get; set; }

        /// <summary>
        /// Foreign key to the owner (user ID).
        /// Right now it's just an int; if Identity integration is added later,
        /// this could map to ApplicationUser.
        /// </summary>
          // CHANGED: string to match AspNetUsers.Id
        [Required]
        [Column(TypeName = "nvarchar(450)")]
        [ForeignKey("AspNetUsers")]
        public string OwnerId { get; set; } = default!;

        /// <summary>
        /// When the list was created (stored in UTC).
        /// Defaults to current UTC time on creation.
        /// </summary>
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the list is private to the owner.
        /// Defaults to true (private).
        /// </summary>
        [Required]
        public bool Private { get; set; } = true;

        /// <summary>
        /// Whether the list is archived or not.
        /// </summary>
        [Column(TypeName = "bit")]
        public bool IsArchived { get; set; } = false;

        /// <summary>
        /// Many-to-many: Recipes associated with this list.
        /// </summary>
        public ICollection<ListRecipe> ListRecipes { get; set; } = new List<ListRecipe>();

        /// <summary>
        /// Many-to-many: Ingredients associated with this list.
        /// </summary>
        public ICollection<ListIngredient> ListIngredients { get; set; } = new List<ListIngredient>();
    }
}
