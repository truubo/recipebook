namespace Recipebook.Models
{
    using Microsoft.EntityFrameworkCore;
    // List.cs
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class List
    {
        [Column(TypeName = "int")]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Name { get; set; } = default!;

        [Required]
        [Column(TypeName = "int")]
        public ListType ListType { get; set; }

        // Owner (User FK)
        [Required]
        [Column(TypeName = "int")]
        public int OwnerId { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        // Grocery lists should be private by default
        [Required]
        [Column(TypeName = "bit")]
        public bool Private { get; set; } = true;

        // Navigation to join tables
        public ICollection<ListRecipe> ListRecipes { get; set; } = new List<ListRecipe>();
        public ICollection<ListIngredient> ListIngredients { get; set; } = new List<ListIngredient>();
    }

}
