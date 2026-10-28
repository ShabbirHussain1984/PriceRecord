using PriceRecord.Services;

namespace PriceRecord.Tests;

public class RobotsTxtPolicyTests
{
    // Excerpt of https://priceoye.pk/robots.txt (October 2026).
    private const string Robots = """
        User-agent: BLEXBot
        User-agent: Apache-HttpClient
        Disallow: /

        User-agent: *
        Disallow: *?
        Disallow: *&
        Disallow: /account/
        Disallow: /orders
        Disallow: /administrateur/
        Allow: /
        Allow: *?page=
        Allow: *?source=ppc

        Sitemap: https://priceoye.pk/sitemap/sitemap-index.xml
        """;

    private const string OurAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 PriceRecordBot/1.0";

    private readonly RobotsTxtPolicy _policy = RobotsTxtPolicy.Parse(Robots, OurAgent);

    [Theory]
    [InlineData("/", true)]
    [InlineData("/mobiles", true)]
    [InlineData("/mobiles?page=2", true)]
    [InlineData("/mobiles/infinix/infinix-smart-20", true)]
    [InlineData("/sitemap/sitemap-category.xml", true)]
    [InlineData("/search?q=iphone", false)]
    [InlineData("/mobiles?sort=price_asc", false)]
    [InlineData("/account/login", false)]
    [InlineData("/orders/123", false)]
    public void StarGroup_RulesApply(string path, bool allowed)
    {
        Assert.Equal(allowed, _policy.IsAllowed(path));
    }

    [Fact]
    public void BlockedAgent_GetsItsOwnGroup()
    {
        var blocked = RobotsTxtPolicy.Parse(Robots, "Apache-HttpClient/4.5");
        Assert.False(blocked.IsAllowed("/mobiles"));
    }

    [Fact]
    public void ProductToken_IsLastNameVersionPair()
    {
        Assert.Equal("pricerecordbot", RobotsTxtPolicy.ProductToken(OurAgent));
    }

    [Fact]
    public void DollarAnchor_MatchesEndOfPath()
    {
        var policy = RobotsTxtPolicy.Parse("User-agent: *\nDisallow: /*.pdf$", OurAgent);
        Assert.False(policy.IsAllowed("/files/a.pdf"));
        Assert.True(policy.IsAllowed("/files/a.pdf.html"));
    }
}
