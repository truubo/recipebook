using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace Recipebook.Infrastructure.Binding
{
    // Parses "1/2", "2 1/4", "0.5", "1.25" into a decimal
    public sealed class FractionDecimalBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext ctx)
        {
            var val = ctx.ValueProvider.GetValue(ctx.ModelName);
            if (val == ValueProviderResult.None) return Task.CompletedTask;

            var s = val.FirstValue?.Trim();
            if (string.IsNullOrWhiteSpace(s))
            {
                ctx.Result = ModelBindingResult.Success(default(decimal));
                return Task.CompletedTask;
            }

            if (TryParseFractionOrDecimal(s, out var d))
            {
                ctx.Result = ModelBindingResult.Success(d);
            }
            else
            {
                ctx.ModelState.AddModelError(ctx.ModelName,
                    "Enter a decimal (e.g., 0.5) or fraction (e.g., 1/2 or 1 1/4).");
            }
            return Task.CompletedTask;
        }

        private static bool TryParseFractionOrDecimal(string input, out decimal result)
        {
            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out result))
                return true;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            decimal total = 0m;

            foreach (var part in parts)
            {
                if (part.Contains('/'))
                {
                    var frac = part.Split('/');
                    if (frac.Length != 2) { result = 0; return false; }
                    if (!decimal.TryParse(frac[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var num)) { result = 0; return false; }
                    if (!decimal.TryParse(frac[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var den)) { result = 0; return false; }
                    if (den == 0) { result = 0; return false; }
                    total += num / den;
                }
                else
                {
                    if (!decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out var whole)) { result = 0; return false; }
                    total += whole;
                }
            }

            result = total;
            return true;
        }
    }
}
