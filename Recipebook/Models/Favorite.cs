using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipebook.Models
{
    /// <summary>
    /// Represents a link between a user and a recipe they have marked as a favorite.
    /// Implements a many-to-many relationship (Users ↔ Recipes) using a join table.
    /// </summary>
    public class Favorite
    {
        /// <summary>
        /// The ID of the user who favorited the recipe.
        /// Matches AspNetUsers.Id from Identity.
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(450)")]
        [ForeignKey("AspNetUsers")]
        public string UserId { get; set; } = default!;

        /// <summary>
        /// The ID of the favorited recipe.
        /// Foreign key to the Recipe table.
        /// </summary>
        [Required]
        public int RecipeId { get; set; }

        /// <summary>
        /// Date/time (UTC) when this recipe was favorited.
        /// Automatically set when created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional navigation property to the associated recipe.
        /// Useful for eager loading.
        /// </summary>
        public Recipe? Recipe { get; set; }
    }
}
