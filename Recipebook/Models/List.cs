namespace Recipebook.Models
{
    // List.cs
    using System.ComponentModel.DataAnnotations;

    public class List
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Name { get; set; } = default!;

        [Required]
        public ListType ListType { get; set; }

        // Owner (User FK)
        public int OwnerId { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        // Grocery lists should be private by default
        public bool Private { get; set; } = true;

        // Navigation to join tables
        public ICollection<ListRecipe> ListRecipes { get; set; } = new List<ListRecipe>();
        public ICollection<ListIngredient> ListIngredients { get; set; } = new List<ListIngredient>();
    }

}
