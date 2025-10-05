using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Recipebook.Models;
using System.Reflection.Emit;

namespace Recipebook.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<List> Lists { get; set; }
        public DbSet<ListRecipe> ListRecipes { get; set; }
        public DbSet<ListIngredient> ListIngredients { get; set; }
        public DbSet<Recipe> Recipe { get; set; } = default!; // singular to match _context.Recipe
        public DbSet<Category> Category { get; set; } = default!;
        public DbSet<Ingredient> Ingredient { get; set; } = default!;
        public DbSet<CategoryRecipe> CategoryRecipes { get; set; }
        public DbSet<IngredientRecipe> IngredientRecipes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ListRecipe: avoid duplicates and wire relationships
            builder.Entity<ListRecipe>()
                .HasIndex(lr => new { lr.ListId, lr.RecipeId })
                .IsUnique();

            builder.Entity<ListRecipe>()
                .HasOne(lr => lr.List)
                .WithMany(l => l.ListRecipes)
                .HasForeignKey(lr => lr.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ListRecipe>()
                .HasOne(lr => lr.Recipe)
                .WithMany(r => r.ListRecipes) // added Recipe.ListRecipes, so points here
                .HasForeignKey(lr => lr.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<IngredientRecipe>()
                .Property(ir => ir.Unit)
                .HasConversion<string>() // store enum name as string
                .HasMaxLength(20);
        }
    }
}
