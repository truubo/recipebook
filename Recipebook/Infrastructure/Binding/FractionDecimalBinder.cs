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
                d = System.Math.Round(d, 4, System.MidpointRounding.AwayFromZero);
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
            s = s.Replace("½", "0.5")
                 .Replace("¼", "0.25")
                 .Replace("¾", "0.75")
                 .Replace("⅓", "0.3333")
                 .Replace("⅔", "0.6667")
                 .Replace("⅕", "0.2")
                 .Replace("⅖", "0.4")
                 .Replace("⅗", "0.6")
                 .Replace("⅘", "0.8")
                 .Replace("⅙", "0.1667")
                 .Replace("⅚", "0.8333")
                 .Replace("⅐", "0.142857")
                 .Replace("⅛", "0.125")
                 .Replace("⅜", "0.375")
                 .Replace("⅝", "0.625")
                 .Replace("⅞", "0.875")
                 .Replace("⅑", "0.111111")
                 .Replace("⅒", "0.1");

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

        // ✅ Display-only helper for formatting decimals as readable fractions
        // Usage in Razor: @FractionDecimalBinder.ToFraction(qty)
        public static string ToFraction(decimal value)
        {
            if (value == 0m) return "0";

            var sign = value < 0 ? "-" : "";
            value = System.Math.Abs(value);

            var whole = System.Math.Floor(value);
            var remainder = (double)(value - (decimal)whole);

            const int denom = 16; // round to nearest sixteenth
            var num = (int)System.Math.Round(remainder * denom);

            // carry if rounded to a whole
            if (num == denom)
            {
                whole += 1;
                num = 0;
            }

            if (num == 0) return $"{sign}{whole}";

            // reduce fraction
            int Gcd(int a, int b)
            {
                while (b != 0) { var t = b; b = a % b; a = t; }
                return a;
            }

            var g = Gcd(num, denom);
            num /= g;
            var reducedDenom = denom / g;

            var text = whole == 0
                ? $"{sign}{num}/{reducedDenom}"
                : $"{sign}{whole} {num}/{reducedDenom}";

            // ✅ Convert to pretty glyphs (¼ ½ ¾, etc.)
            return Pretty(text);
        }

        private static string Pretty(string s) => s
                // 1/2, 1/3, 2/3, 1/4, 3/4
                .Replace(" 1/2", " ½").Replace("1/2", "½")
                .Replace(" 1/3", " ⅓").Replace("1/3", "⅓")
                .Replace(" 2/3", " ⅔").Replace("2/3", "⅔")
                .Replace(" 1/4", " ¼").Replace("1/4", "¼")
                .Replace(" 3/4", " ¾").Replace("3/4", "¾")
                // fifths
                .Replace(" 1/5", " ⅕").Replace("1/5", "⅕")
                .Replace(" 2/5", " ⅖").Replace("2/5", "⅖")
                .Replace(" 3/5", " ⅗").Replace("3/5", "⅗")
                .Replace(" 4/5", " ⅘").Replace("4/5", "⅘")
                // sixths
                .Replace(" 1/6", " ⅙").Replace("1/6", "⅙")
                .Replace(" 5/6", " ⅚").Replace("5/6", "⅚")
                // sevenths (only 1/7 exists)
                .Replace(" 1/7", " ⅐").Replace("1/7", "⅐")
                // eighths
                .Replace(" 1/8", " ⅛").Replace("1/8", "⅛")
                .Replace(" 3/8", " ⅜").Replace("3/8", "⅜")
                .Replace(" 5/8", " ⅝").Replace("5/8", "⅝")
                .Replace(" 7/8", " ⅞").Replace("7/8", "⅞")
                // ninths (only 1/9 exists)
                .Replace(" 1/9", " ⅑").Replace("1/9", "⅑")
                // tenths (only 1/10 exists)
                .Replace(" 1/10", " ⅒").Replace("1/10", "⅒");
    }
}
