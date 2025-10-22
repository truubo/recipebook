using Recipebook.Models;

namespace Recipebook.Services.Interfaces
{
    public interface ITextNormalizationService
    {
        string ToTitleCase(string input);
        string NormalizeCategory(string input);
        string NormalizeRecipeTitle(string input);
        string NormalizeIngredientName(string input);
        string ToLowerForDisplay(string input);
        string UnitToDisplay(Unit unit, decimal quantity = 1);
        string FormatIngredientDisplay(string quantity, Unit unit, string ingredientName);
    }
}