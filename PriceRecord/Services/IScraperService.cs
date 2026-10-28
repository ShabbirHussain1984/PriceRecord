using PriceRecord.Models;

namespace PriceRecord.Services;

public interface IScraperService
{
    /// <summary>Base delay between requests; can be changed while a run is in progress.</summary>
    int DelayMs { get; set; }

    Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct);

    /// <summary>True when a stopped run left resume information behind.</summary>
    bool HasResumeState { get; }

    /// <summary>
    /// Scrapes the selected categories and saves each page's products to the database as it goes.
    /// Never throws for individual page/product failures; those are logged and counted.
    /// </summary>
    Task<ScrapeSummary> RunAsync(ScrapeOptions options, IProgress<ScrapeProgress> progress, CancellationToken ct);
}
