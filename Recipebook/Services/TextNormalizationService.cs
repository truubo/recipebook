using System.Globalization;
using Recipebook.Services.Interfaces;

namespace Recipebook.Services
{
    public class TextNormalizationService : ITextNormalizationService
    {
        public string ToTitleCase(string input) => ToSmartTitleCase(input);

        public string NormalizeRecipeTitle(string input) => ToSmartTitleCase(input);

        public string NormalizeCategory(string input) => ToSmartTitleCase(input);

        public string NormalizeIngredientName(string input) => ToSmartTitleCase(input);

        // capitalizes the first letter of each word except for certain small words
        private string ToSmartTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // Words that should remain lowercase unless first word
            string[] smallWords = { "a", "an", "the", "and", "but", "or", "for", "nor",
                                    "on", "at", "to", "from", "by", "of", "in", "with" };

            var words = input.ToLower().Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                if (i == 0 || !smallWords.Contains(words[i]))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }

            return string.Join(' ', words);
        }
    }
}