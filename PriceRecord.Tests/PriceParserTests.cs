using PriceRecord.Services;

namespace PriceRecord.Tests;

public class PriceParserTests
{
    [Theory]
    [InlineData("Rs. 54,999", 54999)]
    [InlineData("Rs.54,999", 54999)]
    [InlineData("Rs54,999", 54999)]            // <sup>Rs</sup>54,999 rendered as text
    [InlineData("Rs 54,999/-", 54999)]
    [InlineData("PKR 1,23,456", 123456)]       // South-Asian digit grouping
    [InlineData("1,234,567", 1234567)]
    [InlineData("  38,999  ", 38999)]
    [InlineData("38999", 38999)]
    [InlineData("38999.00", 38999)]
    [InlineData("Rs. 1,299.50", 1299.50)]
    [InlineData("Rs 54,999", 54999)]      // non-breaking space
    public void ParsePrice_ReadsCommonFormats(string text, double expected)
    {
        Assert.Equal((decimal)expected, PriceParser.ParsePrice(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Rs.")]
    [InlineData("Coming soon")]
    public void ParsePrice_ReturnsNull_WhenNoNumber(string? text)
    {
        Assert.Null(PriceParser.ParsePrice(text));
    }

    [Theory]
    [InlineData("3% OFF", 3)]
    [InlineData("12.5% off", 12.5)]
    [InlineData(" 50 % OFF ", 50)]
    public void ParsePercentage_ReadsBadge(string text, double expected)
    {
        Assert.Equal((decimal)expected, PriceParser.ParsePercentage(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Free delivery")]
    public void ParsePercentage_ReturnsNull_WithoutPercentSign(string? text)
    {
        Assert.Null(PriceParser.ParsePercentage(text));
    }
}
