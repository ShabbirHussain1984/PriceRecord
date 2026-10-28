using PriceRecord.Models;

namespace PriceRecord.Data;

public interface IProductRepository
{
    /// <summary>Connection string used by all calls; set from the UI before a run.</summary>
    string ConnectionString { get; set; }

    /// <summary>Opens a connection and checks that dbo.Products and dbo.PriceHistory exist.</summary>
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct);

    /// <summary>
    /// Upserts the products by ProductUrl, <c>batchSize</c> rows per transaction, writing a PriceHistory row
    /// for new products and for products whose price or discount changed. Sets <see cref="Product.ProductId"/>.
    /// </summary>
    /// <returns>Number of rows saved.</returns>
    Task<int> UpsertAsync(IReadOnlyList<Product> products, int batchSize, CancellationToken ct);
}

public sealed record ConnectionTestResult(bool Success, bool SchemaPresent, string Message);
