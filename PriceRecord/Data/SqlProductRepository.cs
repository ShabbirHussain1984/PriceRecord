using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PriceRecord.Config;
using PriceRecord.Models;

namespace PriceRecord.Data;

public sealed class SqlProductRepository : IProductRepository
{
    // Column limits from CreateDatabase.sql.
    private const int UrlMax = 500;
    private const int NameMax = 300;
    private const int CategoryMax = 150;
    private const int StatusMax = 50;
    private const int ImageMax = 500;

    // UPDATE-then-INSERT under UPDLOCK/HOLDLOCK: no race between the existence check and the insert.
    // A PriceHistory row is written when the product is new or its price/discount changed (NULL-safe compare).
    private const string UpsertSql = """
        DECLARE @Id INT, @OldPrice DECIMAL(18,2), @OldPct DECIMAL(5,2);

        SELECT @Id = ProductId, @OldPrice = PriceAfterDiscount, @OldPct = DiscountPercentage
        FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
        WHERE ProductUrl = @ProductUrl;

        IF @Id IS NULL
        BEGIN
            INSERT INTO dbo.Products
                (ProductUrl, ItemName, ItemDescription, Category, OriginalPrice, DiscountAmount,
                 DiscountPercentage, PriceAfterDiscount, StockStatus, ImageUrl, ScrapedAt)
            VALUES
                (@ProductUrl, @ItemName, @ItemDescription, @Category, @OriginalPrice, @DiscountAmount,
                 @DiscountPercentage, @PriceAfterDiscount, @StockStatus, @ImageUrl, @ScrapedAt);
            SET @Id = CAST(SCOPE_IDENTITY() AS INT);

            INSERT INTO dbo.PriceHistory (ProductId, PriceAfterDiscount, DiscountPercentage, CapturedAt)
            VALUES (@Id, @PriceAfterDiscount, @DiscountPercentage, @ScrapedAt);
        END
        ELSE
        BEGIN
            UPDATE dbo.Products SET
                ItemName = @ItemName,
                ItemDescription = COALESCE(@ItemDescription, ItemDescription),
                Category = @Category,
                OriginalPrice = @OriginalPrice,
                DiscountAmount = @DiscountAmount,
                DiscountPercentage = @DiscountPercentage,
                PriceAfterDiscount = @PriceAfterDiscount,
                StockStatus = @StockStatus,
                ImageUrl = COALESCE(@ImageUrl, ImageUrl),
                ScrapedAt = @ScrapedAt,
                UpdatedAt = SYSDATETIME()
            WHERE ProductId = @Id;

            IF EXISTS (SELECT @OldPrice, @OldPct EXCEPT SELECT @PriceAfterDiscount, @DiscountPercentage)
                INSERT INTO dbo.PriceHistory (ProductId, PriceAfterDiscount, DiscountPercentage, CapturedAt)
                VALUES (@Id, @PriceAfterDiscount, @DiscountPercentage, @ScrapedAt);
        END

        SELECT @Id;
        """;

    private readonly ILogger<SqlProductRepository> _log;

    public SqlProductRepository(AppSettings settings, ILogger<SqlProductRepository> log)
    {
        ConnectionString = settings.ConnectionString;
        _log = log;
    }

    public string ConnectionString { get; set; }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync(ct);
            var tables = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name IN ('Products','PriceHistory')",
                cancellationToken: ct));
            return tables == 2
                ? new ConnectionTestResult(true, true, $"Connected to {conn.DataSource}/{conn.Database}. Tables found.")
                : new ConnectionTestResult(true, false, $"Connected to {conn.DataSource}/{conn.Database}, but dbo.Products/dbo.PriceHistory are missing. Run CreateDatabase.sql.");
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
        {
            return new ConnectionTestResult(false, false, ex.Message);
        }
    }

    public async Task<int> UpsertAsync(IReadOnlyList<Product> products, int batchSize, CancellationToken ct)
    {
        if (products.Count == 0) return 0;
        batchSize = Math.Max(1, batchSize);

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        var saved = 0;
        foreach (var batch in products.Chunk(batchSize))
        {
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                foreach (var p in batch)
                {
                    p.ProductId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                        UpsertSql, ToParameters(p), tx, cancellationToken: ct));
                }
                await tx.CommitAsync(ct);
                saved += batch.Length;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        _log.LogDebug("Upserted {Count} products", saved);
        return saved;
    }

    private static DynamicParameters ToParameters(Product p)
    {
        var args = new DynamicParameters();
        args.Add("ProductUrl", Truncate(p.ProductUrl, UrlMax), DbType.String, size: UrlMax);
        args.Add("ItemName", Truncate(p.ItemName, NameMax), DbType.String, size: NameMax);
        args.Add("ItemDescription", p.ItemDescription, DbType.String, size: -1);
        args.Add("Category", Truncate(p.Category, CategoryMax), DbType.String, size: CategoryMax);
        args.Add("OriginalPrice", p.OriginalPrice, DbType.Decimal, precision: 18, scale: 2);
        args.Add("DiscountAmount", p.DiscountAmount, DbType.Decimal, precision: 18, scale: 2);
        args.Add("DiscountPercentage", p.DiscountPercentage, DbType.Decimal, precision: 5, scale: 2);
        args.Add("PriceAfterDiscount", p.PriceAfterDiscount, DbType.Decimal, precision: 18, scale: 2);
        args.Add("StockStatus", Truncate(p.StockStatus, StatusMax), DbType.String, size: StatusMax);
        args.Add("ImageUrl", Truncate(p.ImageUrl, ImageMax), DbType.String, size: ImageMax);
        args.Add("ScrapedAt", p.ScrapedAt, DbType.DateTime2);
        return args;
    }

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
