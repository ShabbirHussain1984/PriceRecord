using Microsoft.Extensions.Logging;
using PriceRecord.Config;
using PriceRecord.Data;
using PriceRecord.Models;

namespace PriceRecord.Services;

public sealed class PriceOyeScraper : IScraperService
{
    private readonly IPageFetcher _fetcher;
    private readonly PriceOyePageParser _parser;
    private readonly IProductRepository _repository;
    private readonly IResumeStore _resume;
    private readonly AppSettings _settings;
    private readonly Selectors _selectors;
    private readonly ILogger<PriceOyeScraper> _log;

    public PriceOyeScraper(
        IPageFetcher fetcher,
        PriceOyePageParser parser,
        IProductRepository repository,
        IResumeStore resume,
        AppSettings settings,
        Selectors selectors,
        ILogger<PriceOyeScraper> log)
    {
        _fetcher = fetcher;
        _parser = parser;
        _repository = repository;
        _resume = resume;
        _settings = settings;
        _selectors = selectors;
        _log = log;
    }

    public int DelayMs
    {
        get => _fetcher.DelayMs;
        set => _fetcher.DelayMs = value;
    }

    public bool HasResumeState => _resume.Load() is not null;

    public async Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct)
    {
        try
        {
            var html = await _fetcher.GetStringAsync(_settings.BaseUrl, ct);
            var categories = _parser.ParseCategories(html);
            if (categories.Count > 0)
            {
                _log.LogInformation("Loaded {Count} categories from the home page", categories.Count);
                return categories;
            }
            _log.LogWarning("No category data on the home page; falling back to the category sitemap");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning("Home page category load failed ({Message}); falling back to the category sitemap", ex.Message);
        }

        var sitemapUrl = new Uri(new Uri(_settings.BaseUrl), _selectors.SitemapCategoryPath).ToString();
        var xml = await _fetcher.GetStringAsync(sitemapUrl, ct);
        var fromSitemap = _parser.ParseCategorySitemap(xml);
        _log.LogInformation("Loaded {Count} categories from {Url}", fromSitemap.Count, sitemapUrl);
        return fromSitemap;
    }

    public async Task<ScrapeSummary> RunAsync(ScrapeOptions options, IProgress<ScrapeProgress> progress, CancellationToken ct)
    {
        DelayMs = options.DelayMs;
        var state = PrepareResumeState(options);
        var categories = options.Categories;
        var saved = 0;
        var productsFailed = 0;
        var pagesFailed = 0;
        var hadIncompleteCategory = false;

        _log.LogInformation("Run started: {Count} categories, delay {Delay} ms, max pages {MaxPages}, product pages {Details}",
            categories.Count, options.DelayMs, options.MaxPagesPerCategory == 0 ? "all" : options.MaxPagesPerCategory,
            options.FetchProductDetails ? "on" : "off");

        try
        {
            for (var c = 0; c < categories.Count; c++)
            {
                var category = categories[c];
                if (state.CompletedCategories.Contains(category.Slug))
                {
                    _log.LogInformation("Skipping {Category}: completed in the previous run", category.Name);
                    continue;
                }

                var page = state.LastCompletedPage.TryGetValue(category.Slug, out var last) ? last + 1 : 1;
                if (page > 1)
                    _log.LogInformation("Resuming {Category} at page {Page}", category.Name, page);

                int? totalPages = null;
                var consecutiveFailures = 0;
                var gaveUp = false;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    if (options.MaxPagesPerCategory > 0 && page > options.MaxPagesPerCategory) break;
                    if (totalPages.HasValue && page > totalPages.Value) break;

                    var url = _parser.BuildCategoryPageUrl(category.Slug, page);
                    ListingPage listing;
                    try
                    {
                        listing = await LoadListingAsync(url, category.Slug, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                    {
                        pagesFailed++;
                        _log.LogError("Page failed after retries, skipping: {Url} — {Message}", url, ex.Message);
                        // Without a known page count, or after repeated failures, move on to the next category.
                        if (totalPages is null || ++consecutiveFailures >= 3)
                        {
                            _log.LogWarning("Giving up on {Category} for this run; it will be retried on resume", category.Name);
                            gaveUp = true;
                            break;
                        }
                        page++;
                        continue;
                    }

                    consecutiveFailures = 0;
                    totalPages = listing.LastPage ?? totalPages;
                    if (listing.Items.Count == 0)
                    {
                        _log.LogInformation("{Category} page {Page}: no products, category finished", category.Name, page);
                        break;
                    }

                    var (products, failed) = await BuildProductsAsync(listing.Items, category, options.FetchProductDetails, ct);
                    productsFailed += failed;

                    var savedThisPage = await SaveAsync(products, ct);
                    if (savedThisPage < products.Count) productsFailed += products.Count - savedThisPage;
                    saved += savedThisPage;

                    state.LastCompletedPage[category.Slug] = page;
                    _resume.Save(state);

                    var pagesForPercent = EffectiveTotal(totalPages, options.MaxPagesPerCategory, page);
                    progress.Report(new ScrapeProgress
                    {
                        CategoryIndex = c + 1,
                        CategoryCount = categories.Count,
                        CategoryName = category.Name,
                        Page = page,
                        TotalPages = pagesForPercent,
                        ProductsSaved = saved,
                        NewProducts = savedThisPage > 0 ? products : Array.Empty<Product>(),
                        Percent = (int)Math.Clamp((c + (double)page / Math.Max(1, pagesForPercent)) / categories.Count * 100, 0, 100),
                    });
                    _log.LogInformation("{Category} page {Page}/{Total}: {Count} products saved ({Source})",
                        category.Name, page, pagesForPercent, savedThisPage, listing.FromJson ? "JSON" : "HTML");

                    if (!listing.LastPage.HasValue && options.MaxPagesPerCategory == 0 && page >= 500) break; // safety net
                    page++;
                }

                if (!gaveUp)
                {
                    state.CompletedCategories.Add(category.Slug);
                    state.LastCompletedPage.Remove(category.Slug);
                    _resume.Save(state);
                }
                else
                {
                    hadIncompleteCategory = true;
                }
                progress.Report(new ScrapeProgress
                {
                    CategoryIndex = c + 1,
                    CategoryCount = categories.Count,
                    CategoryName = category.Name,
                    ProductsSaved = saved,
                    Percent = (int)((c + 1) * 100.0 / categories.Count),
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _log.LogWarning("Run stopped by user. {Saved} products saved; the next run can resume where this one stopped", saved);
            return new ScrapeSummary(saved, productsFailed, pagesFailed, Cancelled: true);
        }

        if (hadIncompleteCategory)
            _log.LogWarning("Some categories could not be finished; tick 'Resume previous run' to retry them");
        else
            _resume.Clear();
        _log.LogInformation("Run finished: {Saved} products saved, {Failed} products failed, {Pages} pages failed",
            saved, productsFailed, pagesFailed);
        return new ScrapeSummary(saved, productsFailed, pagesFailed, Cancelled: false);
    }

    private ResumeState PrepareResumeState(ScrapeOptions options)
    {
        var slugs = options.Categories.Select(c => c.Slug).ToList();
        if (options.Resume && _resume.Load() is { } previous)
        {
            previous.CategorySlugs = slugs;
            return previous;
        }
        _resume.Clear();
        return new ResumeState { CategorySlugs = slugs };
    }

    private static int EffectiveTotal(int? totalPages, int maxPages, int currentPage)
    {
        var total = totalPages ?? currentPage;
        if (maxPages > 0) total = Math.Min(total, maxPages);
        return Math.Max(total, currentPage);
    }

    private async Task<ListingPage> LoadListingAsync(string url, string categorySlug, CancellationToken ct)
    {
        var html = await _fetcher.GetStringAsync(url, ct);
        var listing = _parser.ParseListing(html, categorySlug);
        // A page with embedded data but no products (e.g. a landing page) is genuinely empty; only
        // pages with no server-side data at all are worth rendering in a browser.
        if (listing.Items.Count > 0 || listing.HasEmbeddedData || !_settings.UsePlaywrightFallback)
            return listing;

        _log.LogInformation("No product data in static HTML of {Url}; rendering with Playwright", url);
        var rendered = await _fetcher.GetRenderedAsync(url, ct);
        return _parser.ParseListing(rendered, categorySlug);
    }

    private async Task<(List<Product> Products, int Failed)> BuildProductsAsync(
        List<ListingItem> items, CategoryInfo category, bool fetchDetails, CancellationToken ct)
    {
        var results = new Product?[items.Count];
        var failed = 0;
        var scrapedAt = DateTime.Now;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, items.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(_settings.MaxParallelRequests, 1, 3), CancellationToken = ct },
            async (i, token) =>
            {
                var item = items[i];
                ProductDetails? details = null;
                if (fetchDetails)
                {
                    try
                    {
                        details = await LoadDetailsAsync(item.ProductUrl, token);
                    }
                    catch (RobotsDisallowedException)
                    {
                        _log.LogWarning("robots.txt disallows {Url}; saving listing data only", item.ProductUrl);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref failed);
                        _log.LogError("Product skipped after retries: {Url} — {Message}", item.ProductUrl, ex.Message);
                        return;
                    }
                }
                results[i] = ToProduct(item, details, category, scrapedAt);
            });

        return (results.Where(p => p is not null).Select(p => p!).ToList(), failed);
    }

    private async Task<ProductDetails> LoadDetailsAsync(string url, CancellationToken ct)
    {
        var html = await _fetcher.GetStringAsync(url, ct);
        var details = _parser.ParseProductPage(html);
        if (details.Description is null && _settings.UsePlaywrightFallback)
            details = _parser.ParseProductPage(await _fetcher.GetRenderedAsync(url, ct));
        return details;
    }

    private Product ToProduct(ListingItem item, ProductDetails? details, CategoryInfo category, DateTime scrapedAt)
    {
        var salePrice = item.SalePrice ?? details?.OfferPrice;
        var available = item.IsLaunched && item.IsInStock;
        var calc = PriceParser.Calculate(item.RetailPrice, salePrice, available, _settings.KeepPriceForOutOfStock);

        var status = !item.IsLaunched ? StockStatuses.Upcoming
            : !item.IsInStock ? StockStatuses.OutOfStock
            : calc.HasPrice ? StockStatuses.InStock
            : StockStatuses.NoPrice;

        if (calc.DiscountPercentage.HasValue && !PriceParser.BadgeMatches(item.BadgePercentage, calc.DiscountPercentage))
        {
            _log.LogWarning("Discount badge mismatch for {Name}: site shows {Badge}%, calculated {Calculated}% ({Original} → {Sale})",
                item.ItemName, item.BadgePercentage, calc.DiscountPercentage, calc.OriginalPrice, calc.PriceAfterDiscount);
        }

        return new Product
        {
            ProductUrl = item.ProductUrl,
            ItemName = item.ItemName,
            ItemDescription = details?.Description ?? item.SpecText,
            Category = category.Name,
            OriginalPrice = calc.OriginalPrice,
            DiscountAmount = calc.DiscountAmount,
            DiscountPercentage = calc.DiscountPercentage,
            PriceAfterDiscount = calc.PriceAfterDiscount,
            StockStatus = status,
            ImageUrl = item.ImageUrl ?? details?.ImageUrl,
            ScrapedAt = scrapedAt,
        };
    }

    private async Task<int> SaveAsync(List<Product> products, CancellationToken ct)
    {
        if (products.Count == 0) return 0;
        try
        {
            // A page that was fully fetched is saved even if Stop is pressed meanwhile.
            return await _repository.UpsertAsync(products, _settings.BatchSize, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Database save failed for {Count} products", products.Count);
            ct.ThrowIfCancellationRequested();
            return 0;
        }
    }
}
