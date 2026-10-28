using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PriceRecord.Config;

namespace PriceRecord.Services;

public interface IHtmlRenderer
{
    Task<string> RenderAsync(string url, CancellationToken ct);
}

/// <summary>
/// Headless Chromium fallback for pages whose products are only rendered by JavaScript.
/// The browser is started on first use and reused. Requires "playwright.ps1 install chromium" once (see README).
/// </summary>
public sealed class PlaywrightRenderer : IHtmlRenderer, IAsyncDisposable, IDisposable
{
    private readonly AppSettings _settings;
    private readonly ILogger<PlaywrightRenderer> _log;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    public PlaywrightRenderer(AppSettings settings, ILogger<PlaywrightRenderer> log)
    {
        _settings = settings;
        _log = log;
    }

    public async Task<string> RenderAsync(string url, CancellationToken ct)
    {
        var context = await GetContextAsync(ct);
        var page = await context.NewPageAsync();
        try
        {
            using var registration = ct.Register(() => _ = page.CloseAsync());
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _settings.RequestTimeoutSeconds * 1000f * 2,
            });
            ct.ThrowIfCancellationRequested();
            return await page.ContentAsync();
        }
        finally
        {
            if (!page.IsClosed)
                await page.CloseAsync();
        }
    }

    private async Task<IBrowserContext> GetContextAsync(CancellationToken ct)
    {
        if (_context is not null) return _context;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_context is not null) return _context;
            _log.LogInformation("Starting headless Chromium for JavaScript-rendered pages");
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions { UserAgent = _settings.UserAgent });
            return _context;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>The DI container is disposed synchronously when the app exits; run the async cleanup off the UI thread.</summary>
    public void Dispose() => Task.Run(() => DisposeAsync().AsTask()).GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.DisposeAsync();
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _initLock.Dispose();
    }
}
