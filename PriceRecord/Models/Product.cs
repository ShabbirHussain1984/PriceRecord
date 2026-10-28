namespace PriceRecord.Models;

public sealed class Product
{
    public int ProductId { get; set; }
    public string ProductUrl { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }
    public string? Category { get; set; }
    public decimal? OriginalPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? PriceAfterDiscount { get; set; }
    public string? StockStatus { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime ScrapedAt { get; set; }
}

public static class StockStatuses
{
    public const string InStock = "In Stock";
    public const string OutOfStock = "Out of Stock";
    public const string Upcoming = "Upcoming";
    public const string NoPrice = "No Price";
}
