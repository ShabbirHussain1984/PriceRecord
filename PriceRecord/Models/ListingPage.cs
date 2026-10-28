namespace PriceRecord.Models;

/// <summary>A product as read from a category listing page, before price rules are applied.</summary>
public sealed class ListingItem
{
    public string ProductUrl { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? SpecText { get; set; }
    public decimal? RetailPrice { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? BadgePercentage { get; set; }
    public bool IsInStock { get; set; } = true;
    public bool IsLaunched { get; set; } = true;
    public string? ImageUrl { get; set; }
}

public sealed class ListingPage
{
    public List<ListingItem> Items { get; } = new();

    /// <summary>Total pages in the category; null when the page did not say.</summary>
    public int? LastPage { get; set; }

    /// <summary>True when items came from embedded JSON, false when from HTML cards.</summary>
    public bool FromJson { get; set; }

    /// <summary>True when the page contained the embedded <c>window.product_data</c> JSON at all.</summary>
    public bool HasEmbeddedData { get; set; }
}

/// <summary>Data read from an individual product page.</summary>
public sealed class ProductDetails
{
    public string? Description { get; set; }
    public decimal? OfferPrice { get; set; }
    public bool? IsInStock { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Where a stopped run should continue.</summary>
public sealed class ResumeState
{
    public List<string> CategorySlugs { get; set; } = new();
    public List<string> CompletedCategories { get; set; } = new();

    /// <summary>Category slug → last page whose products were saved.</summary>
    public Dictionary<string, int> LastCompletedPage { get; set; } = new();

    public DateTime UpdatedAt { get; set; }
}
