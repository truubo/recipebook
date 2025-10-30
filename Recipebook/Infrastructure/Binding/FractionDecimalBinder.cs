using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Recipebook.Infrastructure.Binding
{
    // Parses "1/2", "2 1/4", "0.5", "1.25" (and "½", "1,25") into a decimal
    public sealed class FractionDecimalBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext ctx)
        {
            var val = ctx.ValueProvider.GetValue(ctx.ModelName);
            if (val == ValueProviderResult.None) return Task.CompletedTask;

            var s = val.FirstValue?.Trim();
            if (string.IsNullOrWhiteSpace(s))
            {
                // keep your existing behavior; change to null if target is nullable
                ctx.Result = ModelBindingResult.Success(default(decimal));
                return Task.CompletedTask;
            }

            s = Normalize(s);

            if (TryParseFractionOrDecimal(s, out var d))
            {
                // optional: round to 4 places
                d = Math.Round(d, 4, MidpointRounding.AwayFromZero);
                ctx.Result = ModelBindingResult.Success(d);
            }
            else
            {
                ctx.ModelState.AddModelError(ctx.ModelName,
                    "Enter a decimal (e.g., 0.5) or fraction (e.g., 1/2 or 1 1/4).");
            }
            return Task.CompletedTask;
        }

        private static string Normalize(string s)
        {
            // Unicode vulgar fractions → ASCII
            s = s.Replace("½", "1/2").Replace("¼", "1/4").Replace("¾", "3/4")
                 .Replace("⅓", "1/3").Replace("⅔", "2/3")
                 .Replace("⅛", "1/8").Replace("⅜", "3/8")
                 .Replace("⅝", "5/8").Replace("⅞", "7/8");

            // Comma decimals → dot (so 1,25 works)
            s = s.Replace(',', '.');

            // Tidy whitespace and around slashes
            s = Regex.Replace(s, @"\s+", " ");     // collapse multi spaces
            s = Regex.Replace(s, @"\s*/\s*", "/"); // trim spaces around '/'
            return s.Trim();
        }

        private static bool TryParseFractionOrDecimal(string input, out decimal result)
        {
            result = 0m;

            // Support leading sign for mixed/fraction (e.g., -1 1/4, -3/8)
            var sign = 1m;
            if (input.StartsWith("+")) input = input[1..].TrimStart();
            else if (input.StartsWith("-")) { sign = -1m; input = input[1..].TrimStart(); }

            // 1) Plain decimal first
            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            {
                result = sign * dec;
                return true;
            }

            // 2) Mixed number: "W N/D" (e.g., "1 1/4")
            var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 2 && tokens[1].Contains('/'))
            {
                if (decimal.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) &&
                    TryParseSimpleFraction(tokens[1], out var frac))
                {
                    result = sign * (whole + frac);
                    return true;
                }
            }

            // 3) Simple fraction: "N/D"
            if (input.Contains('/') && TryParseSimpleFraction(input, out var onlyFrac))
            {
                result = sign * onlyFrac;
                return true;
            }

            return false;
        }

        private static bool TryParseSimpleFraction(string s, out decimal value)
        {
            value = 0m;
            var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            if (!decimal.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var num)) return false;
            if (!decimal.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var den)) return false;
            if (den == 0) return false;

            value = num / den;
            return true;
        }
    }
}
