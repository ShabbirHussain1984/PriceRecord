using PriceRecord.Services;

namespace PriceRecord.Tests;

public class DiscountCalculationTests
{
    [Fact]
    public void Normal_Discount_IsCalculated()
    {
        var result = PriceParser.Calculate(39999m, 38999m);

        Assert.Equal(39999m, result.OriginalPrice);
        Assert.Equal(1000m, result.DiscountAmount);
        Assert.Equal(2.50m, result.DiscountPercentage);   // 1000 / 39999 * 100 = 2.50006…
        Assert.Equal(38999m, result.PriceAfterDiscount);
    }

    [Fact]
    public void Percentage_IsRoundedToTwoDecimals()
    {
        var result = PriceParser.Calculate(656999m, 325999m);

        Assert.Equal(331000m, result.DiscountAmount);
        Assert.Equal(50.38m, result.DiscountPercentage);  // 50.380…
    }

    [Fact]
    public void Percentage_RoundsMidpointAwayFromZero()
    {
        // 1 / 800 * 100 = 0.125 exactly → 0.13
        var result = PriceParser.Calculate(800m, 799m);
        Assert.Equal(0.13m, result.DiscountPercentage);
    }

    [Fact]
    public void Equal_Prices_MeanNoDiscount()
    {
        var result = PriceParser.Calculate(3829m, 3829m);

        Assert.Equal(3829m, result.OriginalPrice);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0m, result.DiscountPercentage);
        Assert.Equal(3829m, result.PriceAfterDiscount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Missing_Original_MeansNoDiscount(int? original)
    {
        var result = PriceParser.Calculate(original, 4750m);

        Assert.Equal(4750m, result.OriginalPrice);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0m, result.DiscountPercentage);
        Assert.Equal(4750m, result.PriceAfterDiscount);
    }

    [Fact]
    public void Missing_Sale_UsesOriginal()
    {
        var result = PriceParser.Calculate(5000m, null);

        Assert.Equal(5000m, result.OriginalPrice);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(5000m, result.PriceAfterDiscount);
    }

    [Fact]
    public void Original_Lower_Than_Sale_IsTreatedAsNoDiscount()
    {
        // Seen on the site: retail_price 1999, lowest_price 2349.
        var result = PriceParser.Calculate(1999m, 2349m);

        Assert.Equal(2349m, result.OriginalPrice);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0m, result.DiscountPercentage);
        Assert.Equal(2349m, result.PriceAfterDiscount);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    [InlineData(-5, null)]
    public void Missing_Price_GivesAllNulls(int? original, int? sale)
    {
        var result = PriceParser.Calculate(original, sale);

        Assert.False(result.HasPrice);
        Assert.Null(result.OriginalPrice);
        Assert.Null(result.DiscountAmount);
        Assert.Null(result.DiscountPercentage);
        Assert.Null(result.PriceAfterDiscount);
    }

    [Fact]
    public void Parsed_Comma_Prices_FeedTheCalculation()
    {
        var result = PriceParser.Calculate(PriceParser.ParsePrice("Rs. 1,39,999"), PriceParser.ParsePrice("Rs 1,34,999"));

        Assert.Equal(139999m, result.OriginalPrice);
        Assert.Equal(5000m, result.DiscountAmount);
        Assert.Equal(3.57m, result.DiscountPercentage);
        Assert.Equal(134999m, result.PriceAfterDiscount);
    }

    [Fact]
    public void OutOfStock_GivesNulls_ByDefault()
    {
        var result = PriceParser.Calculate(39999m, 38999m, isAvailable: false, keepPriceWhenUnavailable: false);
        Assert.Equal(PriceCalculation.Empty, result);
    }

    [Fact]
    public void OutOfStock_KeepsPrice_WhenConfigured()
    {
        var result = PriceParser.Calculate(39999m, 38999m, isAvailable: false, keepPriceWhenUnavailable: true);
        Assert.Equal(38999m, result.PriceAfterDiscount);
    }

    [Theory]
    [InlineData(3, 2.50, true)]      // site rounds 2.5 up to 3 → within 1 point
    [InlineData(4, 2.50, false)]
    [InlineData(null, 2.50, true)]   // no badge, nothing to validate
    [InlineData(10, 9.01, true)]
    public void BadgeMatches_UsesOnePointTolerance(int? badge, double calculated, bool expected)
    {
        Assert.Equal(expected, PriceParser.BadgeMatches(badge, (decimal)calculated));
    }
}
