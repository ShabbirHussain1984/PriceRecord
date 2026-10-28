using PriceRecord.Config;
using PriceRecord.Services;

namespace PriceRecord.Tests;

/// <summary>Uses trimmed-down pages that mirror the structure observed on priceoye.pk.</summary>
public class PriceOyePageParserTests
{
    private readonly PriceOyePageParser _parser = new(new Selectors(), "https://priceoye.pk/");

    private const string ListingWithJson = """
        <html><body>
        <div class="product-list">
          <div class="productBox b-productBox"><a href="https://priceoye.pk/mobiles/infinix/infinix-smart-20" class="ga-dataset">
            <div class="image-box"><img src="https://images.priceoye.pk/infinix-smart-20-100x100.webp" class="product-thumbnail-img product-thumbnail"></div>
            <div class="detail-box"><div class="text-box"><h4 class="p-title p-title-center bold h5"><!----> Infinix Smart 20</h4></div>
            <div class="pricing-setting"><div class="price-box h2"><span><sup>Rs</sup>38,999</span></div>
            <div class="price-diff"><div class="price-diff-retail"><span><sup>Rs</sup>39,999</span></div><div class="price-diff-saving">3% OFF </div></div></div></div></a></div>
          <div class="productBox b-productBox"><a href="https://priceoye.pk/mobiles/vivo/vivo-y32" class="ga-dataset">
            <div class="image-box"><div id="stock-badge" class="out-of-stock-label"></div><img src="https://images.priceoye.pk/vivo-y32-100x100.webp" class="product-thumbnail-img product-thumbnail"></div>
            <div class="detail-box"><div class="text-box"><h4 class="p-title">Vivo Y32</h4></div>
            <div class="pricing-setting"><div class="price-box h2"><span><sup>Rs</sup>73,999</span></div></div></div></a></div>
        </div>
        <script>
            window.product_data = {"categories":[{"id":1,"name":"Mobiles","slug":"mobiles"},{"id":4,"name":"Fridge","slug":"refrigerators"}],
              "dataSet":{"page":"categorypage"},
              "pages":{"current_page":1,"last_page":12,"per_page":35,"total":402},
              "product_combined_array":[{"_id":"15610","_source":{"product_id":15610,"spec_text":"Screen Size 6.78 inches &amp; 5200 mAh"}}],
              "dataset":[
                {"product_id":15610,"product_title":"Infinix Smart 20","product_image":"[\"infinix-smart-20-270x270.webp\"]","product_slug":"infinix-smart-20","product_status":"launched","stock_status":1,"product_brand_slug":"infinix","product_category_slug":"mobiles","lowest_price":38999,"retail_price":"39999"},
                {"product_id":99,"product_title":"Vivo Y32","product_image":"[\"vivo-y32-270x270.webp\"]","product_slug":"vivo-y32","product_status":"launched","stock_status":0,"product_brand_slug":"vivo","product_category_slug":"mobiles","lowest_price":73999,"retail_price":0}
              ]};
            window.trackingConfig = {};
        </script>
        </body></html>
        """;

    [Fact]
    public void ParseListing_PrefersEmbeddedJson()
    {
        var page = _parser.ParseListing(ListingWithJson);

        Assert.True(page.FromJson);
        Assert.Equal(12, page.LastPage);
        Assert.Equal(2, page.Items.Count);

        var smart = page.Items[0];
        Assert.Equal("https://priceoye.pk/mobiles/infinix/infinix-smart-20", smart.ProductUrl);
        Assert.Equal("Infinix Smart 20", smart.ItemName);
        Assert.Equal(38999m, smart.SalePrice);
        Assert.Equal(39999m, smart.RetailPrice);
        Assert.Equal(3m, smart.BadgePercentage);                  // merged from the HTML card
        Assert.Equal("Screen Size 6.78 inches & 5200 mAh", smart.SpecText);
        Assert.Equal("https://images.priceoye.pk/infinix-smart-20-270x270.webp", smart.ImageUrl);
        Assert.True(smart.IsInStock);

        var vivo = page.Items[1];
        Assert.False(vivo.IsInStock);
        Assert.Equal(0m, vivo.RetailPrice);
    }

    [Fact]
    public void ParseListing_FallsBackToHtmlCards_WithoutJson()
    {
        var htmlOnly = ListingWithJson[..ListingWithJson.IndexOf("<script>", StringComparison.Ordinal)] +
                       """<div class="page-item"><a class="number">2</a></div><div class="page-item"><a class="number">7</a></div></body></html>""";

        var page = _parser.ParseListing(htmlOnly);

        Assert.False(page.FromJson);
        Assert.Equal(7, page.LastPage);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("Infinix Smart 20", page.Items[0].ItemName);
        Assert.Equal(38999m, page.Items[0].SalePrice);
        Assert.Equal(39999m, page.Items[0].RetailPrice);
        Assert.Equal(3m, page.Items[0].BadgePercentage);
        Assert.True(page.Items[0].IsInStock);
        Assert.False(page.Items[1].IsInStock);
        Assert.Null(page.Items[1].RetailPrice);
    }

    [Fact]
    public void ParseListing_HtmlFallback_DropsCardsOfOtherCategories()
    {
        var htmlOnly = ListingWithJson[..ListingWithJson.IndexOf("<script>", StringComparison.Ordinal)] + "</body></html>";

        Assert.Equal(2, _parser.ParseListing(htmlOnly, "mobiles").Items.Count);
        Assert.Empty(_parser.ParseListing(htmlOnly, "auto").Items);   // landing page showing motorcycles
    }

    [Fact]
    public void ParseCategories_ReadsHomePageJson()
    {
        var categories = _parser.ParseCategories(ListingWithJson);

        Assert.Equal(2, categories.Count);
        Assert.Equal("Fridge", categories[1].Name);
        Assert.Equal("refrigerators", categories[1].Slug);
    }

    [Fact]
    public void ParseCategorySitemap_KeepsSingleSegmentUrls()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://priceoye.pk/mobiles</loc></url>
              <url><loc>https://priceoye.pk/mobiles/samsung</loc></url>
              <url><loc>https://priceoye.pk/smart-watches</loc></url>
            </urlset>
            """;

        var categories = _parser.ParseCategorySitemap(xml);

        Assert.Equal(new[] { "mobiles", "smart-watches" }, categories.Select(c => c.Slug));
        Assert.Equal("Smart Watches", categories[1].Name);
    }

    [Fact]
    public void ParseProductPage_ReadsJsonLdProduct()
    {
        const string html = """
            <html><head>
            <meta name="description" content="Buy it at the lowest price">
            <script type="application/ld+json">{"@context":"https://schema.org/","@type":"WebSite","name":"Priceoye"}</script>
            <script type="application/ld+json">
            {"@context":"http:\/\/schema.org","@type":"Product","name":"Infinix Smart 20",
             "image":"https:\/\/images.priceoye.pk\/infinix-smart-20-270x270.webp",
             "offers":{"@type":"Offer","price":"38999.00","priceCurrency":"PKR","availability":"http:\/\/schema.org\/InStock"},
             "description":"The Infinix Smart 20 has a 6.78 inches screen."}
            </script></head><body></body></html>
            """;

        var details = _parser.ParseProductPage(html);

        Assert.Equal("The Infinix Smart 20 has a 6.78 inches screen.", details.Description);
        Assert.Equal(38999m, details.OfferPrice);
        Assert.True(details.IsInStock);
        Assert.Equal("https://images.priceoye.pk/infinix-smart-20-270x270.webp", details.ImageUrl);
    }

    [Fact]
    public void ParseProductPage_StripsHtmlFromDescription()
    {
        const string html = """
            <html><head><script type="application/ld+json">
            {"@type":"Product","name":"Jersey","description":"<div class=\"column\"><p>Official <b>PSL</b> jersey<\/p><ul><li>100% polyester<\/li><\/ul><\/div>"}
            </script></head></html>
            """;

        Assert.Equal("Official PSL jersey 100% polyester", _parser.ParseProductPage(html).Description);
    }

    [Fact]
    public void ParseProductPage_FallsBackToMetaDescription()
    {
        const string html = """<html><head><meta name="description" content="Short &amp; sweet"></head></html>""";

        var details = _parser.ParseProductPage(html);

        Assert.Equal("Short & sweet", details.Description);
        Assert.Null(details.IsInStock);
    }

    [Theory]
    [InlineData("mobiles", 1, "https://priceoye.pk/mobiles")]
    [InlineData("mobiles", 3, "https://priceoye.pk/mobiles?page=3")]
    public void BuildCategoryPageUrl_UsesPageQuery(string slug, int page, string expected)
    {
        Assert.Equal(expected, _parser.BuildCategoryPageUrl(slug, page));
    }
}
