# PriceRecord — priceoye.pk price collector

A Windows Forms app (.NET 8) that collects every product listed on <https://priceoye.pk/>, works out
the discount, and saves the results to SQL Server along with a price history.

```
PriceRecord.slnx
CreateDatabase.sql            database + tables (safe to re-run)
PriceRecord/                  WinForms app
  Program.cs                  DI, configuration, Serilog, HttpClient + Polly retry pipeline
  appsettings.json
  Config/    AppSettings.cs, Selectors.cs        ← every site-specific selector lives here
  Models/    Product.cs, ScrapeProgress.cs, ListingPage.cs
  Services/  IScraperService.cs, PriceOyeScraper.cs, PriceOyePageParser.cs, PriceParser.cs,
             PoliteHttpClient.cs, RobotsTxtPolicy.cs, PlaywrightRenderer.cs, ResumeStore.cs, ExportService.cs
  Data/      IProductRepository.cs, SqlProductRepository.cs
  UI/        MainForm.cs, MainForm.Designer.cs, SortableBindingList.cs, UiLogSink.cs
PriceRecord.Tests/            xUnit tests (price parsing, discount rules, page parser, robots.txt)
```

## Setup

1. **Requirements:** Windows, the .NET 8 SDK or newer (the projects target `net8.0-windows`), and SQL Server
   (Express, LocalDB or full).
2. **Create the database:**
   ```
   sqlcmd -S .\SQLEXPRESS -E -C -i CreateDatabase.sql
   ```
   Or open `CreateDatabase.sql` in SSMS and run it. It creates the `PriceRecord` database, plus
   `dbo.Products` and `dbo.PriceHistory`.
3. **Set the connection string** in `PriceRecord/appsettings.json` (`AppSettings:ConnectionString`), or paste it
   into the textbox in the app. Example for SQL Express:
   `Server=.\SQLEXPRESS;Database=PriceRecord;Trusted_Connection=True;TrustServerCertificate=True;`
4. **Build and run:**
   ```
   dotnet build PriceRecord.slnx
   dotnet run --project PriceRecord
   dotnet test PriceRecord.slnx
   ```
   Or open `PriceRecord.slnx` in Visual Studio 2022 (17.13+) and press F5.
5. *(Optional)* **Playwright fallback.** Only needed if you set `UsePlaywrightFallback: true`. After building, install Chromium once:
   ```
   pwsh PriceRecord/bin/Debug/net8.0-windows/playwright.ps1 install chromium
   ```

### NuGet packages

| Package | Used for |
|---|---|
| HtmlAgilityPack 1.11.71 | HTML parsing (XPath) |
| Microsoft.Playwright 1.48.0 | optional headless-browser fallback for JS-rendered pages |
| Microsoft.Extensions.Http 8.0.1 | `IHttpClientFactory` |
| Microsoft.Extensions.Http.Resilience 8.10.0 (Polly 8) | retry with exponential backoff, longer pause on 429, per-attempt timeout |
| Microsoft.Extensions.DependencyInjection 8.0.1 | DI container |
| Microsoft.Extensions.Configuration.Json / .Binder 8.0.x | `appsettings.json` |
| Microsoft.Extensions.Logging 8.0.1, Serilog 4.1.0, Serilog.Extensions.Logging 8.0.0, Serilog.Sinks.File 6.0.0 | logging to file + log box |
| Dapper 2.1.35, Microsoft.Data.SqlClient 5.2.2 | SQL Server access (parameterized) |
| ClosedXML 0.104.1 | Excel export |
| xunit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.11.1 | tests |

## Using the app

1. The category list fills itself from the site at startup. Tick the categories you want, or use **Select All**.
2. **Test Connection** checks that the database is reachable and that the tables exist. **Start** runs the same check first.
3. **Delay (ms)** is the base wait between requests (default 1500). Each wait is randomised between ⅔ and 5⁄3 of it,
   so the default gives 1.0–2.5 s. You can change it while a run is going.
   **Max pages / category**: 0 means all pages. Each page holds about 36 products.
4. **Visit product pages** makes one extra request per product to fetch its short description. With it
   off, the description is the spec summary from the listing (where the site provides one), and runs
   are roughly 30× faster.
5. **Stop** cancels cleanly. Every page that was fully fetched has already been saved, and a resume
   file records the last completed category and page. On the next start **Resume previous run** is ticked
   automatically, and the run continues from that point.
6. The grid fills as pages are saved. Click a column header to sort it, and type in **Filter by name** to filter.
   **Export CSV/Excel…** writes the rows currently shown.
7. Log files are written to `bin/.../logs/pricerecord-YYYYMMDD.log`, at Debug level including every URL fetched.
   The log box shows Information and above: warnings in amber, errors in red.

### Settings (`appsettings.json` → `AppSettings`)

| Key | Default | Meaning |
|---|---|---|
| `ConnectionString` | localhost / PriceRecord | prefilled into the textbox |
| `BaseUrl` | `https://priceoye.pk/` | |
| `UserAgent` | Chrome UA + `PriceRecordBot/1.0` | realistic browser UA with an honest bot token; robots.txt groups are matched on `PriceRecordBot` |
| `DelayMs` | 1500 | base delay (randomised, see above) |
| `MaxPagesPerCategory` | 0 | 0 = all |
| `MaxParallelRequests` | 1 | clamped to 1–3 |
| `BatchSize` | 50 | rows per database transaction |
| `RequestTimeoutSeconds` | 30 | per attempt |
| `MaxRetries` | 4 | for timeouts, network errors, 408, 429, 5xx |
| `TooManyRequestsPauseSeconds` | 60 | pause after a 429 is this × attempt number, or the server's `Retry-After` if that is longer |
| `FetchProductDetails` | true | initial state of "Visit product pages" |
| `KeepPriceForOutOfStock` | false | see the data rules below |
| `UsePlaywrightFallback` | false | render pages in Chromium when the static HTML has no product data |
| `ResumeFile` / `LogDirectory` | `resume.json` / `logs` | relative to the exe |

## Data rules

- Prices such as `Rs. 54,999`, `Rs54,999`, `PKR 1,23,456` or `54999.00` are parsed to `decimal`
  (`PriceParser.ParsePrice`).
- If there is an original price and a lower sale price: `DiscountAmount = Original − Sale`,
  `DiscountPercentage = Round(DiscountAmount / Original × 100, 2)` (midpoint rounded away from zero).
- With no discount (equal prices, original missing or 0, or original *lower* than the sale price,
  which the site does sometimes show): `DiscountAmount = 0`, `DiscountPercentage = 0`, `PriceAfterDiscount = OriginalPrice`.
- The "3% OFF" badge on listing cards is parsed and compared with the calculated percentage. A difference
  of more than 1 point is logged as a warning. The site rounds its badge, so 2.5% shows as "3% OFF".
- Out-of-stock, upcoming and price-less products are **saved, not skipped**, with NULL prices and a
  `StockStatus` of `In Stock`, `Out of Stock`, `Upcoming` or `No Price`. Set `KeepPriceForOutOfStock: true`
  to keep the last listed price for out-of-stock items. This matters for categories such as Motorcycle, where
  every item is listed as out of stock but still shows a price.
- **Upsert by `ProductUrl`.** Inside a transaction, the row is selected `WITH (UPDLOCK, HOLDLOCK)`, then
  updated or inserted. A `PriceHistory` row is added when a product is new, or when its `PriceAfterDiscount` or
  `DiscountPercentage` changed (NULL-safe comparison). `ScrapedAt` holds the latest scrape time and
  `UpdatedAt` is set on every update.
- A page or product that still fails after retries is logged and skipped. A category is abandoned after 3
  consecutive page failures, and a resume run tries it again.

## What the site looks like (assumptions made, checked October 2026)

Checked by hand before writing the selectors:

- **robots.txt:** for `User-agent: *`, every URL containing `?` or `&` is disallowed **except `?page=`**, and
  `/account/`, `/orders` and `/administrateur/` are blocked. The app downloads and checks robots.txt before
  every request (`RobotsTxtPolicy`). If robots.txt returns 5xx, the run stops rather than guessing.
- **Sitemaps:** `/sitemap/sitemap-index.xml` links to `sitemap-category.xml` (category and brand pages) and
  `sitemap-product.xml` (about 17,000 URLs, many of them discontinued models). The app does **not** crawl the
  product sitemap. It walks the category listings, which contain only products the site currently lists.
  The category sitemap is used only as a fallback for the category list.
- **Rendering:** pages are server-side rendered (Vue SSR). Every page contains a script line
  `window.product_data = {...};` with the page data as JSON, so Playwright is not needed today.
- **Categories:** `window.product_data.categories` on the home page, e.g. `{ "name": "Fridge", "slug": "refrigerators" }`.
- **Listing pages** are `/{slug}?page=N`. The pagination buttons have no `href`, because JavaScript handles them,
  but the `?page=` URL works directly. In the JSON:
  - `dataset[]` holds the products on the page: `product_title`, `product_category_slug`, `product_brand_slug`,
    `product_slug`, `lowest_price` (the selling price), `retail_price` (the original price, `0` when there is none),
    `stock_status` (1/0), `product_status` (`launched`, …) and `product_image` (a JSON-encoded file list).
  - `product_combined_array[]`, on some categories only, holds the same products with `_source.spec_text`, a
    one-line spec summary.
  - `pages.last_page` gives the total number of pages.
  - Product URL = `https://priceoye.pk/{category}/{brand}/{slug}`, cross-checked against the card's `href`.
  - Image URL = `https://images.priceoye.pk/{file}`.
- **Product pages** have a schema.org `Product` JSON-LD block: `description` (sometimes containing HTML, which is
  stripped) and `offers.price` / `offers.availability`. `<meta name="description">` is the fallback.
- **HTML fallback** (used if the JSON disappears): cards are `div.productBox`, with the title in `.p-title`, the sale
  price in `.price-box`, the original price in `.price-diff-retail`, the badge in `.price-diff-saving`, the image in
  `img.product-thumbnail`, and out of stock marked by `.out-of-stock-label`.
- `/auto` is a landing page that shows motorcycle and e-bike cards. The fallback keeps only cards whose URL is
  under the category being scraped, so those products are not saved a second time as "Auto".

## When the site layout changes

All site knowledge is in **`PriceRecord/Config/Selectors.cs`**: JSON key names, XPath selectors and URL patterns.
Every property can also be overridden **without recompiling**, from the `Selectors` section of `appsettings.json`:

```json
"Selectors": {
  "JsonProducts": "products",
  "JsonSalePrice": "sale_price",
  "CardNode": "//li[contains(@class,'product-card')]",
  "CardSalePrice": ".//span[@class='price-now']"
}
```

Suggested workflow:

1. Open a category page in a browser, then *View source* (not DevTools, which shows the DOM after JavaScript has run).
2. Search for `window.product_data`. If it is still there, compare its key names with the `Json*` properties.
   If it is gone, update the `Card*` XPath expressions, or turn on `UsePlaywrightFallback` if products now
   only appear after JavaScript runs.
3. Save the page source, paste a trimmed version into `PriceRecord.Tests/PriceOyePageParserTests.cs`, and adjust
   the expected values. `dotnet test` will then show whether the parser reads the new layout.
4. Run the app with **Max pages = 1** on one category and check the grid and the log. Look for
   "badge mismatch" warnings, which usually mean the original and sale prices have been swapped or mis-selected.
