namespace Recipebook.Models
{
    public class ListRecipe
    {
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; }

        public int ListId { get; set; }
        public List List { get; set; }
    }
}
