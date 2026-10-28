# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

WinForms app (.NET 8, `net8.0-windows`) that scrapes all products from https://priceoye.pk/ and upserts them into SQL Server with a price history. `README.md` holds the user-facing setup notes, the settings table and the observed site structure.

## Commands

Run these from the solution root (`D:\Projects\Personal\PriceRecord`, the folder that contains `PriceRecord.slnx`):

```
dotnet build PriceRecord.slnx
dotnet test PriceRecord.slnx
dotnet test PriceRecord.Tests --filter "FullyQualifiedName~DiscountCalculationTests"        # one class
dotnet test PriceRecord.Tests --filter "FullyQualifiedName~PriceParserTests.ParsePrice_ReadsCommonFormats"  # one test
dotnet run --project PriceRecord
sqlcmd -S .\SQLEXPRESS -E -C -i CreateDatabase.sql      # idempotent; creates DB "PriceRecord"
```

- There is no linter. Keep builds at 0 warnings (`dotnet build --no-incremental` shows them).
- The local machine runs SQL Express as `.\SQLEXPRESS`. For DB experiments, use a throwaway database (e.g. sed `PriceRecord` → `PriceRecordSmokeTest` in the script) and drop it afterwards. The other databases on that server belong to the user.
- Python and Node are not installed. Executables built into temp folders (e.g. `dotnet run file.cs`) are blocked by Windows Application Control. To try code against real data, use a temporary xUnit test (`Assert.Fail(summary)` is a handy way to print) and delete it afterwards.
- Visual Studio often has the solution open and may rewrite `PriceRecord.csproj.user`. Leave that file alone.

## Architecture

Composition happens in `PriceRecord/Program.cs`:
- It binds `AppSettings` and `Selectors` from `appsettings.json` and configures Serilog: a file sink, plus `UiLogSink`, which forwards events to the form's RichTextBox.
- It registers everything as singletons.
- It sets up the named `HttpClient` "priceoye" with a Polly v8 pipeline through `Microsoft.Extensions.Http.Resilience`. The pipeline retries with exponential backoff, uses a much longer `DelayGenerator` pause on 429, and applies a timeout per attempt. `HttpClient.Timeout` is deliberately infinite so long 429 pauses aren't cut short.

Request flow for a run (`PriceOyeScraper.RunAsync`, executed via `Task.Run` from `MainForm`, with progress reported through `IProgress<ScrapeProgress>`):

1. **`PoliteHttpClient` (`IPageFetcher`)** is the only gateway to the site.
   - Checks `RobotsTxtPolicy` before every request. Groups are matched on the UA's last product token, `PriceRecordBot`. If robots.txt returns 5xx, it refuses to crawl.
   - Waits a random delay of ⅔–5⁄3 × `DelayMs`.
   - Limits concurrency to `MaxParallelRequests` (clamped 1–3).
   - `GetRenderedAsync` goes through the same gate to `PlaywrightRenderer`, used only when `UsePlaywrightFallback` is set.
2. **`PriceOyePageParser`** is pure (string in, models out).
   - Site pages are server-rendered and embed `window.product_data = {...};`. `ExtractEmbeddedJson` parses exactly one JSON value after that marker with `Utf8JsonReader` + `JsonDocument.ParseValue`, because trailing script follows it.
   - The JSON has both `dataSet` and `dataset` keys, so always use case-sensitive `JsonDocument` access, never case-insensitive POCO deserialization.
   - Listing items come from JSON `dataset[]`. `spec_text` is joined from `product_combined_array[]._source` by `product_id`. The discount badge and canonical href are merged in from the HTML cards.
   - HTML cards are the fallback, filtered to URLs under the category being scraped (`/auto` is a landing page that shows other categories' products).
   - Product pages are read from the schema.org `Product` JSON-LD (whose description may contain HTML, which gets stripped), falling back to the meta description.
3. **`PriceParser`** is pure and static, and holds all price/discount rules. It parses texts like `Rs. 54,999` and `1,23,456`, and computes discount amount and percentage (`Math.Round(…, 2, AwayFromZero)`).
   - When the original price is ≤ the sale price, or one price is missing, there is no discount.
   - Unavailable products get NULL prices unless `KeepPriceForOutOfStock` is set.
   - The badge tolerance is 1 percentage point.
   - The stock-status strings are in `StockStatuses`.
4. **`SqlProductRepository`** (Dapper) runs a T-SQL upsert per product, `BatchSize` rows per transaction.
   - It does SELECT `WITH (UPDLOCK, HOLDLOCK)` → UPDATE or INSERT.
   - It inserts a `PriceHistory` row when the product is new, or when `PriceAfterDiscount`/`DiscountPercentage` changed. The comparison is NULL-safe, via `EXCEPT`.
   - Column length limits are duplicated as constants there, and must match `CreateDatabase.sql`.
5. **Resume:** `JsonResumeStore` writes `resume.json` next to the exe after each saved page, recording the completed categories and the last completed page per category.
   - A page counts as complete only after its products are saved. A fully fetched page is saved even if Stop is pressed (`CancellationToken.None`).
   - A category abandoned after 3 consecutive page failures is not marked complete, and the resume file is kept.

Failure policy: individual page and product failures are logged and counted, and never thrown. Only cancellation ends `RunAsync` early, returning `Cancelled = true`.

## Site-specific knowledge

Every JSON key, XPath and URL pattern lives in `PriceRecord/Config/Selectors.cs`. Each one can also be overridden from the `Selectors` section of `appsettings.json`. When the site changes, update the selectors there rather than hard-coding them in the parser, and update the trimmed fixture HTML in `PriceRecord.Tests/PriceOyePageParserTests.cs`. robots.txt allows only `?page=` among query strings, so new URLs with other query parameters will be refused by the fetcher.

## Gotchas

- `using HtmlAgilityPack;` in this WinForms project needs `using HtmlDocument = HtmlAgilityPack.HtmlDocument;`, which avoids the clash with `System.Windows.Forms.HtmlDocument`.
- For JSON inside C# raw-string test fixtures, write `\"` escapes, not `\u0022`. The file-writing tool decodes `\u0022` into a literal quote, which corrupts the fixture.
- `PlaywrightRenderer` implements `IDisposable` as well as `IAsyncDisposable`, because the DI container is disposed synchronously when the app exits.
