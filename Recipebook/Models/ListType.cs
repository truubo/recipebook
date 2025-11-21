using System.ComponentModel;

namespace Recipebook.Models
{
    public enum ListType
    {
        [Description("Used for meal plans, favorites, etc.")]
        Recipes = 0,

        [Description("Used for shopping Lists, collections, etc.")]
        Ingredients = 1
    }
}