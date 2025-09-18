using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Recipebook.Models
{
    public class Ingredient
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<IngredientRecipe> IngredientRecipes { get; set; }
    }
}
