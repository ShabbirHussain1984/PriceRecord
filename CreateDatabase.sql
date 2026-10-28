/*
    PriceRecord database for the priceoye.pk scraper.
    Run in SSMS / Azure Data Studio, or:  sqlcmd -S localhost -E -i CreateDatabase.sql
    Safe to re-run: every object is created only if it does not exist yet.
*/

IF DB_ID(N'PriceRecord') IS NULL
    CREATE DATABASE PriceRecord;
GO

USE PriceRecord;
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        ProductId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
        ProductUrl         NVARCHAR(500)  NOT NULL CONSTRAINT UQ_Products_ProductUrl UNIQUE,
        ItemName           NVARCHAR(300)  NOT NULL,
        ItemDescription    NVARCHAR(MAX)  NULL,
        Category           NVARCHAR(150)  NULL,
        OriginalPrice      DECIMAL(18,2)  NULL,
        DiscountAmount     DECIMAL(18,2)  NULL,
        DiscountPercentage DECIMAL(5,2)   NULL,
        PriceAfterDiscount DECIMAL(18,2)  NULL,
        StockStatus        NVARCHAR(50)   NULL,
        ImageUrl           NVARCHAR(500)  NULL,
        ScrapedAt          DATETIME2      NOT NULL CONSTRAINT DF_Products_ScrapedAt DEFAULT SYSDATETIME(),
        UpdatedAt          DATETIME2      NULL
    );

    CREATE INDEX IX_Products_Category ON dbo.Products (Category);
END
GO

IF OBJECT_ID(N'dbo.PriceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PriceHistory
    (
        PriceHistoryId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PriceHistory PRIMARY KEY,
        ProductId          INT            NOT NULL
            CONSTRAINT FK_PriceHistory_Products REFERENCES dbo.Products (ProductId) ON DELETE CASCADE,
        PriceAfterDiscount DECIMAL(18,2)  NULL,
        DiscountPercentage DECIMAL(5,2)   NULL,
        CapturedAt         DATETIME2      NOT NULL CONSTRAINT DF_PriceHistory_CapturedAt DEFAULT SYSDATETIME()
    );

    CREATE INDEX IX_PriceHistory_Product_CapturedAt ON dbo.PriceHistory (ProductId, CapturedAt DESC);
END
GO

/*
    Upsert used by the application (SqlProductRepository), shown here for reference.
    The app runs it once per product, 50 products per transaction:

    SELECT ... FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE ProductUrl = @ProductUrl;
    -- not found  -> INSERT dbo.Products + INSERT dbo.PriceHistory
    -- found      -> UPDATE dbo.Products (UpdatedAt = SYSDATETIME())
    --               + INSERT dbo.PriceHistory only when PriceAfterDiscount or DiscountPercentage changed

    Useful queries:

    -- Latest price drops
    SELECT TOP 50 p.ItemName, p.Category, h.PriceAfterDiscount, h.CapturedAt
    FROM dbo.PriceHistory h JOIN dbo.Products p ON p.ProductId = h.ProductId
    ORDER BY h.CapturedAt DESC;

    -- Biggest discounts in stock
    SELECT TOP 50 ItemName, OriginalPrice, PriceAfterDiscount, DiscountPercentage
    FROM dbo.Products WHERE StockStatus = N'In Stock'
    ORDER BY DiscountPercentage DESC;
*/
