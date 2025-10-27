using Recipebook.Models;
using Recipebook.Services.Interfaces;
using System.Globalization;

namespace Recipebook.Services
{
    public class TextNormalizationService : ITextNormalizationService
    {
        // Converts input to title case using custom capitalization rules
        public string ToTitleCase(string input) => ToSmartTitleCase(input);

        // Normalizes recipe titles for consistent capitalization
        public string NormalizeRecipeTitle(string input) => ToSmartTitleCase(input);

        // Normalizes category names with the same capitalization logic
        public string NormalizeCategory(string input) => ToSmartTitleCase(input);

        // Normalizes ingredient names (e.g., "olive oil" → "Olive Oil")
        public string NormalizeIngredientName(string input) => ToSmartTitleCase(input);

        // Capitalizes the first letter of each word except small words (like "and", "of", etc.)
        private string ToSmartTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Words that remain lowercase unless they're the first word
            string[] smallWords = { "a", "an", "the", "and", "but", "or", "for", "nor",
                                    "on", "at", "to", "from", "by", "of", "in", "with", "is" };

            var words = input.ToLower().Split(' ');

            // Loop through each word, capitalizing when appropriate
            for (int i = 0; i < words.Length; i++)
            {
                if (i == 0 || !smallWords.Contains(words[i]))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }

            // Rebuild the sentence with spaces
            return string.Join(' ', words);
        }

        // Converts input to lowercase for consistent display
        public string ToLowerForDisplay(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? input : input.ToLower();
        }

        // Returns the unit name in lowercase and pluralizes it if quantity > 1
        public string UnitToDisplay(Unit unit, decimal quantity = 1)
        {
            var unitStr = unit.ToString().ToLower();
            return quantity == 1 ? unitStr : unitStr + "s";
        }

        // Builds a formatted string like "2 cups of sugar" or "1 tablespoon of butter"
        public string FormatIngredientDisplay(string quantity, Unit unit, string ingredientName)
        {
            // Safely parse quantity to handle invalid inputs
            if (!decimal.TryParse(quantity, out var qtyValue))
            {
                qtyValue = 1;
            }

            var unitDisplay = UnitToDisplay(unit, qtyValue);
            var nameDisplay = ToLowerForDisplay(ingredientName);

            // Include unit if present, otherwise just show quantity and ingredient
            if (!string.IsNullOrWhiteSpace(unitDisplay))
                return $"{quantity} {unitDisplay} of {nameDisplay}";

            return $"{quantity} {nameDisplay}";
        }
    }
}