using System.ComponentModel;

namespace Recipebook.Models
{
    public enum ListType
    {
        [Description("Meal plan containing selected recipes")]
        Recipes = 0,

        [Description("Grocery list of ingredients to shop for")]
        Ingredients = 1
    }
}
