namespace Recipebook.Models
{
    public class ListIngredient
    {
        public int Id { get; set; }

        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; }

        public int ListId { get; set; }
        public List List { get; set; }

        public int Quantity { get; set; }
    }
}
