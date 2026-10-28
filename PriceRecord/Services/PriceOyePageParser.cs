using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using HtmlAgilityPack;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using PriceRecord.Config;
using PriceRecord.Models;

namespace PriceRecord.Services;

/// <summary>
/// Turns priceoye.pk HTML into models. Pure (string in, objects out) so it can be tested with saved pages.
/// Embedded JSON is preferred; HTML cards are used for the discount badge and as a fallback.
/// </summary>
public sealed class PriceOyePageParser
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly Selectors _s;
    private readonly Uri _baseUri;

    public PriceOyePageParser(Selectors selectors, string baseUrl)
    {
        _s = selectors;
        _baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    }

    public string BuildCategoryPageUrl(string categorySlug, int page)
    {
        var categoryUrl = new Uri(_baseUri, categorySlug).ToString();
        return page <= 1 ? categoryUrl : string.Format(CultureInfo.InvariantCulture, _s.CategoryPageUrlFormat, categoryUrl, page);
    }

    // ---- Categories ---------------------------------------------------------------------------

    public List<CategoryInfo> ParseCategories(string html)
    {
        var result = new List<CategoryInfo>();
        using var doc = ExtractEmbeddedJson(html);
        if (doc is null || !TryGetArray(doc.RootElement, _s.JsonCategories, out var categories))
            return result;

        foreach (var c in categories.EnumerateArray())
        {
            var name = GetString(c, _s.JsonCategoryName);
            var slug = GetString(c, _s.JsonCategorySlug);
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(slug) && result.All(r => r.Slug != slug))
                result.Add(new CategoryInfo(WebUtility.HtmlDecode(name).Trim(), slug.Trim('/')));
        }
        return result;
    }

    /// <summary>Single-segment URLs from the category sitemap (e.g. https://priceoye.pk/mobiles).</summary>
    public List<CategoryInfo> ParseCategorySitemap(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "loc")
            .Select(e => Uri.TryCreate(e.Value.Trim(), UriKind.Absolute, out var u) ? u : null)
            .Where(u => u is not null && u.Host == _baseUri.Host)
            .Select(u => u!.AbsolutePath.Trim('/'))
            .Where(p => p.Length > 0 && !p.Contains('/'))
            .Distinct()
            .Select(slug => new CategoryInfo(ToTitle(slug), slug))
            .ToList();
    }

    // ---- Listing pages ---------------------------------------------------------------------------

    /// <param name="html">Listing page HTML.</param>
    /// <param name="categorySlug">
    /// When given, HTML-card fallback keeps only products under /{categorySlug}/. Landing pages such as /auto
    /// show cards of other categories (motorcycles, e-bikes) that must not be re-labelled.
    /// </param>
    public ListingPage ParseListing(string html, string? categorySlug = null)
    {
        var page = new ListingPage();
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);
        var cards = ParseCards(htmlDoc);

        using (var json = ExtractEmbeddedJson(html))
        {
            if (json is not null)
            {
                page.HasEmbeddedData = true;
                ReadJsonListing(json.RootElement, page);
            }
        }

        if (page.Items.Count > 0)
        {
            page.FromJson = true;
            MergeCardData(page.Items, cards);
        }
        else
        {
            page.Items.AddRange(categorySlug is null ? cards : cards.Where(c => IsUnderCategory(c.ProductUrl, categorySlug)));
        }

        page.LastPage ??= ParsePaginationFromHtml(htmlDoc);
        return page;
    }

    private void ReadJsonListing(JsonElement root, ListingPage page)
    {
        if (TryGetObject(root, _s.JsonPages, out var pages) && TryGetInt(pages, _s.JsonLastPage, out var lastPage) && lastPage > 0)
            page.LastPage = lastPage;

        // product_combined_array carries spec_text; dataset is present on every listing page.
        var specs = new Dictionary<string, string>();
        var hitSources = new List<JsonElement>();
        if (TryGetArray(root, _s.JsonProductsWithSpecs, out var hits))
        {
            foreach (var hit in hits.EnumerateArray())
            {
                var source = TryGetObject(hit, _s.JsonHitSource, out var s) ? s : hit;
                hitSources.Add(source);
                var id = GetString(source, _s.JsonProductId);
                var spec = GetString(source, _s.JsonSpecText);
                if (id is not null && !string.IsNullOrWhiteSpace(spec))
                    specs[id] = spec;
            }
        }

        IEnumerable<JsonElement> products = TryGetArray(root, _s.JsonProducts, out var dataset)
            ? dataset.EnumerateArray()
            : hitSources;

        foreach (var p in products)
        {
            var item = ReadJsonProduct(p);
            if (item is null) continue;
            var id = GetString(p, _s.JsonProductId);
            if (item.SpecText is null && id is not null && specs.TryGetValue(id, out var spec))
                item.SpecText = CleanText(spec);
            page.Items.Add(item);
        }
    }

    private ListingItem? ReadJsonProduct(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;

        var title = GetString(p, _s.JsonTitle);
        var slug = GetString(p, _s.JsonProductSlug);
        var brand = GetString(p, _s.JsonBrandSlug);
        var category = GetString(p, _s.JsonCategorySlugOfProduct);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(category))
            return null;

        var path = string.IsNullOrWhiteSpace(brand) ? $"{category}/{slug}" : $"{category}/{brand}/{slug}";
        var stock = GetString(p, _s.JsonStockStatus);
        var status = GetString(p, _s.JsonProductStatus);
        var spec = GetString(p, _s.JsonSpecText);

        return new ListingItem
        {
            ProductUrl = new Uri(_baseUri, path).ToString(),
            ItemName = CleanText(title)!,
            SpecText = string.IsNullOrWhiteSpace(spec) ? null : CleanText(spec),
            SalePrice = GetDecimal(p, _s.JsonSalePrice),
            RetailPrice = GetDecimal(p, _s.JsonRetailPrice),
            IsInStock = stock is null || stock == "1" || stock.Equals("true", StringComparison.OrdinalIgnoreCase),
            IsLaunched = status is null || status.Equals(_s.LaunchedStatusValue, StringComparison.OrdinalIgnoreCase),
            ImageUrl = ResolveImage(GetString(p, _s.JsonImage)),
        };
    }

    /// <summary>product_image is a JSON-encoded string: ["a-270x270.webp", ...] or {"0":"a.jpg","colors":{...}}.</summary>
    private string? ResolveImage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string? file = raw.Trim();
        if (file.StartsWith('[') || file.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(file, JsonOptions);
                var root = doc.RootElement;
                file = root.ValueKind switch
                {
                    JsonValueKind.Array => root.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                    JsonValueKind.Object => root.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).Select(x => x.Value.GetString()).FirstOrDefault(),
                    _ => null,
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
        if (string.IsNullOrWhiteSpace(file)) return null;
        return Uri.TryCreate(file, UriKind.Absolute, out var abs) ? abs.ToString() : new Uri(new Uri(_s.ImageBaseUrl), file).ToString();
    }

    private List<ListingItem> ParseCards(HtmlDocument doc)
    {
        var items = new List<ListingItem>();
        var nodes = doc.DocumentNode.SelectNodes(_s.CardNode);
        if (nodes is null) return items;

        foreach (var card in nodes)
        {
            var href = card.SelectSingleNode(_s.CardLink)?.GetAttributeValue("href", null);
            var title = CleanText(card.SelectSingleNode(_s.CardTitle)?.InnerText);
            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title)) continue;
            if (!Uri.TryCreate(_baseUri, WebUtility.HtmlDecode(href), out var url)) continue;

            var img = card.SelectSingleNode(_s.CardImage);
            var src = img?.GetAttributeValue("data-src", null) ?? img?.GetAttributeValue("src", null);

            items.Add(new ListingItem
            {
                ProductUrl = url.ToString(),
                ItemName = title,
                SalePrice = PriceParser.ParsePrice(CleanText(card.SelectSingleNode(_s.CardSalePrice)?.InnerText)),
                RetailPrice = PriceParser.ParsePrice(CleanText(card.SelectSingleNode(_s.CardRetailPrice)?.InnerText)),
                BadgePercentage = PriceParser.ParsePercentage(CleanText(card.SelectSingleNode(_s.CardDiscountBadge)?.InnerText)),
                IsInStock = card.SelectSingleNode(_s.CardOutOfStock) is null,
                ImageUrl = string.IsNullOrWhiteSpace(src) ? null : new Uri(_baseUri, src.Trim()).ToString(),
            });
        }
        return items;
    }

    /// <summary>Copies the discount badge from the HTML card and prefers the card's href as the canonical URL.</summary>
    private static void MergeCardData(List<ListingItem> items, List<ListingItem> cards)
    {
        if (cards.Count == 0) return;
        var byUrl = new Dictionary<string, ListingItem>(StringComparer.OrdinalIgnoreCase);
        var bySlug = new Dictionary<string, ListingItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cards)
        {
            byUrl.TryAdd(c.ProductUrl.TrimEnd('/'), c);
            bySlug.TryAdd(LastSegment(c.ProductUrl), c);
        }

        foreach (var item in items)
        {
            if (!byUrl.TryGetValue(item.ProductUrl.TrimEnd('/'), out var card) &&
                bySlug.TryGetValue(LastSegment(item.ProductUrl), out card))
            {
                item.ProductUrl = card.ProductUrl;
            }
            if (card is null) continue;
            item.BadgePercentage = card.BadgePercentage;
            item.ImageUrl ??= card.ImageUrl;
        }
    }

    private int? ParsePaginationFromHtml(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes(_s.PaginationNumbers);
        if (nodes is null) return null;
        var numbers = nodes
            .Select(n => int.TryParse(CleanText(n.InnerText), NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToList();
        return numbers.Count > 0 ? numbers.Max() : null;
    }

    // ---- Product pages ---------------------------------------------------------------------------

    public ProductDetails ParseProductPage(string html)
    {
        var details = new ProductDetails();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scripts = doc.DocumentNode.SelectNodes(_s.JsonLdScripts);
        if (scripts is not null)
        {
            foreach (var script in scripts)
            {
                var product = FindJsonLdProduct(script.InnerText);
                if (product is null) continue;
                using (product)
                {
                    var p = product.RootElement;
                    details.Description = CleanText(StripTags(GetString(p, "description")));
                    details.ImageUrl = GetString(p, "image");
                    if (TryGetObject(p, "offers", out var offers) ||
                        (TryGetArray(p, "offers", out var offerList) && offerList.GetArrayLength() > 0 && (offers = offerList[0]).ValueKind == JsonValueKind.Object))
                    {
                        details.OfferPrice = GetDecimal(offers, "price") ?? GetDecimal(offers, "lowPrice");
                        var availability = GetString(offers, "availability");
                        if (availability is not null)
                            details.IsInStock = !availability.Contains(_s.OutOfStockAvailabilityToken, StringComparison.OrdinalIgnoreCase);
                    }
                }
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(details.Description))
            details.Description = CleanText(doc.DocumentNode.SelectSingleNode(_s.MetaDescription)?.GetAttributeValue("content", null));

        return details;
    }

    private JsonDocument? FindJsonLdProduct(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json.Trim(), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        var candidates = new List<JsonElement>();
        void Collect(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Array)
                foreach (var x in e.EnumerateArray()) Collect(x);
            else if (e.ValueKind == JsonValueKind.Object)
            {
                candidates.Add(e);
                if (TryGetArray(e, "@graph", out var graph)) Collect(graph);
            }
        }
        Collect(doc.RootElement);

        var match = candidates.FirstOrDefault(c => GetString(c, "@type") == _s.JsonLdProductType);
        if (match.ValueKind == JsonValueKind.Undefined)
        {
            doc.Dispose();
            return null;
        }
        var raw = match.GetRawText();
        doc.Dispose();
        return JsonDocument.Parse(raw, JsonOptions);
    }

    // ---- JSON helpers ----------------------------------------------------------------------------

    /// <summary>Finds the <c>window.product_data = {...};</c> assignment and parses just that one JSON value.</summary>
    public JsonDocument? ExtractEmbeddedJson(string html)
    {
        var marker = html.IndexOf(_s.EmbeddedJsonMarker, StringComparison.Ordinal);
        if (marker < 0) return null;
        var start = html.IndexOf('{', marker + _s.EmbeddedJsonMarker.Length);
        if (start < 0) return null;

        var bytes = Encoding.UTF8.GetBytes(html.AsSpan(start).ToString());
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        try
        {
            return JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetArray(JsonElement e, string name, out JsonElement value)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array
           || Fail(out value);

    private static bool TryGetObject(JsonElement e, string name, out JsonElement value)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object
           || Fail(out value);

    private static bool Fail(out JsonElement value)
    {
        value = default;
        return false;
    }

    private static string? GetString(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static bool TryGetInt(JsonElement e, string name, out int value)
    {
        value = 0;
        var s = GetString(e, name);
        return s is not null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Prices arrive both as numbers (38999) and strings ("39999", "38,999").</summary>
    private static decimal? GetDecimal(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return v.ValueKind == JsonValueKind.String ? PriceParser.ParsePrice(v.GetString()) : null;
    }

    /// <summary>Some product descriptions are HTML fragments; keep their text, one space between elements.</summary>
    private static string? StripTags(string? text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('<')) return text;
        var fragment = new HtmlDocument();
        fragment.LoadHtml(text);
        var parts = fragment.DocumentNode
            .DescendantsAndSelf()
            .Where(n => n.NodeType == HtmlNodeType.Text && n.ParentNode?.Name is not ("script" or "style"))
            .Select(n => n.InnerText.Trim())
            .Where(s => s.Length > 0);
        return string.Join(" ", parts);
    }

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var decoded = WebUtility.HtmlDecode(WebUtility.HtmlDecode(text)); // some fields are double-encoded (&amp;amp;)
        var sb = new StringBuilder(decoded.Length);
        var lastWasSpace = false;
        foreach (var ch in decoded)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }
        var result = sb.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    private static bool IsUnderCategory(string url, string categorySlug)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) &&
           u.AbsolutePath.StartsWith("/" + categorySlug.Trim('/') + "/", StringComparison.OrdinalIgnoreCase);

    private static string LastSegment(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Segments[^1].Trim('/') : url;

    private static string ToTitle(string slug)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace('-', ' '));
}
