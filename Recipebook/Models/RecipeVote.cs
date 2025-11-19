using Microsoft.EntityFrameworkCore;

namespace Recipebook.Models
{
    // One vote per (RecipeId, UserId)
    [Index(nameof(RecipeId), nameof(UserId), IsUnique = true)]
    public class RecipeVote
    {
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        // ASP.NET Identity user id
        public string UserId { get; set; } = null!;

        // true = like, false = dislike
        public bool IsLike { get; set; }
    }
}
