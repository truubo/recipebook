namespace Recipebook.Models
{
    public class IngredientRecipe
    {
        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; }

        public int Quantity { get; set; }

        public string Unit { get; set; }
    }
}
