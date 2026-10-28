namespace PriceRecord.Config;

/// <summary>
/// Every site-specific selector, JSON key and URL pattern lives here so a site redesign only
/// needs changes in this file (or overrides in the "Selectors" section of appsettings.json,
/// which binds onto these properties by name).
///
/// Structure observed on priceoye.pk (October 2026):
/// - Pages are server-side rendered (Vue SSR); every page embeds a script line
///   <c>window.product_data = {...};</c> with the page's data as JSON.
/// - Category listing: /{categorySlug}?page=N (robots.txt allows "?page=", disallows other query strings).
///   JSON: "dataset" = array of products on the page; "product_combined_array" (only on some
///   categories) = the same products as Elasticsearch hits whose "_source" also carries "spec_text";
///   "pages.last_page" = number of pages.
/// - Product URL: {BaseUrl}{product_category_slug}/{product_brand_slug}/{product_slug}.
/// - Product page: a schema.org "Product" JSON-LD block with "description" and "offers".
/// </summary>
public sealed class Selectors
{
    // ---- Embedded JSON ----------------------------------------------------------------------
    public string EmbeddedJsonMarker { get; set; } = "window.product_data =";

    public string JsonCategories { get; set; } = "categories";
    public string JsonCategoryName { get; set; } = "name";
    public string JsonCategorySlug { get; set; } = "slug";

    public string JsonProducts { get; set; } = "dataset";
    public string JsonProductsWithSpecs { get; set; } = "product_combined_array";
    public string JsonHitSource { get; set; } = "_source";
    public string JsonPages { get; set; } = "pages";
    public string JsonLastPage { get; set; } = "last_page";

    public string JsonProductId { get; set; } = "product_id";
    public string JsonTitle { get; set; } = "product_title";
    public string JsonProductSlug { get; set; } = "product_slug";
    public string JsonBrandSlug { get; set; } = "product_brand_slug";
    public string JsonCategorySlugOfProduct { get; set; } = "product_category_slug";
    public string JsonSalePrice { get; set; } = "lowest_price";
    public string JsonRetailPrice { get; set; } = "retail_price";
    public string JsonStockStatus { get; set; } = "stock_status";
    public string JsonProductStatus { get; set; } = "product_status";
    public string JsonImage { get; set; } = "product_image";
    public string JsonSpecText { get; set; } = "spec_text";

    /// <summary>Value of <see cref="JsonProductStatus"/> for products that are on sale.</summary>
    public string LaunchedStatusValue { get; set; } = "launched";

    // ---- URLs ---------------------------------------------------------------------------------
    public string ImageBaseUrl { get; set; } = "https://images.priceoye.pk/";
    public string CategoryPageUrlFormat { get; set; } = "{0}?page={1}";
    public string SitemapCategoryPath { get; set; } = "sitemap/sitemap-category.xml";

    // ---- HTML fallback (XPath, used when embedded JSON is missing) -----------------------------
    public string CardNode { get; set; } = "//div[contains(concat(' ', normalize-space(@class), ' '), ' productBox ')]";
    public string CardLink { get; set; } = ".//a[@href]";
    public string CardTitle { get; set; } = ".//*[contains(@class,'p-title')]";
    public string CardSalePrice { get; set; } = ".//div[contains(@class,'price-box')]";
    public string CardRetailPrice { get; set; } = ".//div[contains(@class,'price-diff-retail')]";
    public string CardDiscountBadge { get; set; } = ".//div[contains(@class,'price-diff-saving')]";
    public string CardImage { get; set; } = ".//img[contains(@class,'product-thumbnail')]";
    public string CardOutOfStock { get; set; } = ".//*[contains(@class,'out-of-stock-label')]";
    public string PaginationNumbers { get; set; } = "//div[contains(@class,'page-item')]/*";

    // ---- Product page ---------------------------------------------------------------------------
    public string JsonLdScripts { get; set; } = "//script[@type='application/ld+json']";
    public string JsonLdProductType { get; set; } = "Product";
    public string MetaDescription { get; set; } = "//meta[@name='description']";
    public string OutOfStockAvailabilityToken { get; set; } = "OutOfStock";
}
