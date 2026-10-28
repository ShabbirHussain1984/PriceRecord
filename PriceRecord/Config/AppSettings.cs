namespace PriceRecord.Config;

/// <summary>
/// Values bound from the "AppSettings" section of appsettings.json.
/// </summary>
public sealed class AppSettings
{
    public string ConnectionString { get; set; } =
        "Server=localhost;Database=PriceRecord;Trusted_Connection=True;TrustServerCertificate=True;";

    public string BaseUrl { get; set; } = "https://priceoye.pk/";

    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 PriceRecordBot/1.0";

    /// <summary>Base delay between requests. The actual delay is randomised between 2/3 and 5/3 of this value.</summary>
    public int DelayMs { get; set; } = 1500;

    /// <summary>0 = no limit.</summary>
    public int MaxPagesPerCategory { get; set; }

    /// <summary>Concurrent requests (clamped to 1..3).</summary>
    public int MaxParallelRequests { get; set; } = 1;

    /// <summary>Rows per database transaction.</summary>
    public int BatchSize { get; set; } = 50;

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 4;

    /// <summary>Base pause applied after an HTTP 429 when the server sends no Retry-After header.</summary>
    public int TooManyRequestsPauseSeconds { get; set; } = 60;

    /// <summary>Visit each product page to read its short description (slower, one extra request per product).</summary>
    public bool FetchProductDetails { get; set; } = true;

    /// <summary>When false (default), out-of-stock products are saved with NULL prices.</summary>
    public bool KeepPriceForOutOfStock { get; set; }

    /// <summary>Render pages with headless Chromium when the static HTML contains no product data.</summary>
    public bool UsePlaywrightFallback { get; set; }

    public string ResumeFile { get; set; } = "resume.json";

    public string LogDirectory { get; set; } = "logs";
}
