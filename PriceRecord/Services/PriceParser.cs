using System.Globalization;
using System.Text;

namespace PriceRecord.Services;

/// <summary>Result of applying the discount rules to a pair of prices.</summary>
public sealed record PriceCalculation(
    decimal? OriginalPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    decimal? PriceAfterDiscount)
{
    public static PriceCalculation Empty { get; } = new(null, null, null, null);

    public bool HasPrice => PriceAfterDiscount.HasValue;
}

/// <summary>
/// Pure price parsing and discount arithmetic. No I/O, no state — safe to unit test directly.
/// </summary>
public static class PriceParser
{
    /// <summary>Allowed difference (percentage points) between the site's badge and the calculated discount.</summary>
    public const decimal BadgeTolerance = 1m;

    /// <summary>
    /// Parses texts like "Rs. 54,999", "Rs54,999", "PKR 1,23,456", "54999.00" or "Rs 54,999/-".
    /// Returns null when no number is present.
    /// </summary>
    public static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var sb = new StringBuilder(text.Length);
        var seenDigit = false;
        var seenDot = false;
        foreach (var ch in text)
        {
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
                seenDigit = true;
            }
            else if (ch == '.' && seenDigit && !seenDot)
            {
                // A dot after digits is a decimal separator; the dot in "Rs." comes before any digit.
                sb.Append('.');
                seenDot = true;
            }
            else if (ch == ',' || char.IsWhiteSpace(ch) || ch == ' ')
            {
                // thousands separators (both 54,999 and 1,23,456 styles) and spacing
            }
            else if (seenDigit)
            {
                // Anything else after the number ("/-", "OFF") ends it.
                break;
            }
        }

        var number = sb.ToString().TrimEnd('.');
        if (number.Length == 0)
            return null;

        return decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>Parses a badge like "3% OFF" or "-12.5%" into 3 / 12.5. Returns null when absent.</summary>
    public static decimal? ParsePercentage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Contains('%'))
            return null;
        return ParsePrice(text[..text.IndexOf('%')]);
    }

    /// <summary>
    /// Applies the discount rules.
    /// <list type="bullet">
    /// <item>Original and lower sale price: amount = original − sale, % = round(amount / original × 100, 2).</item>
    /// <item>Only one usable price, or the "original" is not higher than the sale price: no discount,
    /// PriceAfterDiscount = OriginalPrice = the price that is known.</item>
    /// <item>No usable price (null, zero or negative): all values null.</item>
    /// </list>
    /// </summary>
    public static PriceCalculation Calculate(decimal? originalPrice, decimal? salePrice)
    {
        var original = originalPrice > 0 ? originalPrice : null;
        var sale = salePrice > 0 ? salePrice : null;

        if (original is null && sale is null)
            return PriceCalculation.Empty;

        if (original is null || sale is null || sale.Value >= original.Value)
        {
            var price = sale ?? original!.Value;
            return new PriceCalculation(price, 0m, 0m, price);
        }

        var amount = original.Value - sale.Value;
        var percentage = Math.Round(amount / original.Value * 100m, 2, MidpointRounding.AwayFromZero);
        return new PriceCalculation(original.Value, amount, percentage, sale.Value);
    }

    /// <summary>
    /// Applies <see cref="Calculate"/> and then the availability rule: a product that cannot be bought
    /// is stored with null prices unless <paramref name="keepPriceWhenUnavailable"/> is set.
    /// </summary>
    public static PriceCalculation Calculate(decimal? originalPrice, decimal? salePrice, bool isAvailable, bool keepPriceWhenUnavailable)
    {
        if (!isAvailable && !keepPriceWhenUnavailable)
            return PriceCalculation.Empty;
        return Calculate(originalPrice, salePrice);
    }

    /// <summary>True when the badge is absent or within <see cref="BadgeTolerance"/> of the calculated percentage.</summary>
    public static bool BadgeMatches(decimal? badgePercentage, decimal? calculatedPercentage)
    {
        if (badgePercentage is null)
            return true;
        return Math.Abs(badgePercentage.Value - (calculatedPercentage ?? 0m)) <= BadgeTolerance;
    }
}
