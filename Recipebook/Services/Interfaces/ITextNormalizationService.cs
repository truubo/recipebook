namespace Recipebook.Services.Interfaces
{
    public interface ITextNormalizationService
    {
        string ToTitleCase(string input);
        string NormalizeCategory(string input);
        string NormalizeRecipeTitle(string input);
        string NormalizeIngredientName(string input);
    }
}