namespace PriceRecord.Models;

/// <summary>Snapshot reported to the UI through <see cref="IProgress{T}"/>.</summary>
public sealed class ScrapeProgress
{
    public int CategoryIndex { get; init; }
    public int CategoryCount { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int ProductsSaved { get; init; }

    /// <summary>Products saved since the previous report (for the live grid).</summary>
    public IReadOnlyList<Product> NewProducts { get; init; } = Array.Empty<Product>();

    /// <summary>0..100 across the whole run.</summary>
    public int Percent { get; init; }

    public string StatusText =>
        TotalPages > 0
            ? $"Category {CategoryIndex}/{CategoryCount} ({CategoryName}) — Page {Page}/{TotalPages} — {ProductsSaved} products saved"
            : $"Category {CategoryIndex}/{CategoryCount} ({CategoryName}) — {ProductsSaved} products saved";
}

public sealed record CategoryInfo(string Name, string Slug)
{
    public override string ToString() => Name;
}

public sealed class ScrapeOptions
{
    public required IReadOnlyList<CategoryInfo> Categories { get; init; }
    public int DelayMs { get; init; }
    public int MaxPagesPerCategory { get; init; }
    public bool FetchProductDetails { get; init; }
    public bool Resume { get; init; }
}

public sealed record ScrapeSummary(int ProductsSaved, int ProductsFailed, int PagesFailed, bool Cancelled);
