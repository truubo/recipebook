using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Recipebook.Models;

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
        public DbSet<Favorite> Favorites { get; set; } = default!;
        public DbSet<RecipeVote> RecipeVotes { get; set; } = default!;

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
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("varchar(20)");

            // ADD: Favorites mapping (composite key + indexes + FK)
            builder.Entity<Favorite>(cfg =>
            {
                cfg.HasKey(x => new { x.UserId, x.RecipeId }); // composite PK
                cfg.HasIndex(x => x.UserId);
                cfg.HasIndex(x => x.RecipeId);

                cfg.HasOne(x => x.Recipe)
                   .WithMany(r => r.Favorites) // ✅ fixed: wire back to Recipe.Favorites
                   .HasForeignKey(x => x.RecipeId)
                   .OnDelete(DeleteBehavior.Cascade);
            });
        }
        public DbSet<Direction> Direction { get; set; } = default!;
    }
}
