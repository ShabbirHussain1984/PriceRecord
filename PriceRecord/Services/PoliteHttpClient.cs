using Microsoft.Extensions.Logging;
using PriceRecord.Config;

namespace PriceRecord.Services;

public interface IPageFetcher
{
    /// <summary>Base delay between requests; changeable while running.</summary>
    int DelayMs { get; set; }

    /// <summary>Downloads a page as text, honouring robots.txt and the request delay. Retries are applied by the HTTP pipeline.</summary>
    Task<string> GetStringAsync(string url, CancellationToken ct);

    /// <summary>Same as <see cref="GetStringAsync"/> but renders the page in a headless browser (JavaScript executed).</summary>
    Task<string> GetRenderedAsync(string url, CancellationToken ct);

    Task<bool> IsAllowedAsync(string url, CancellationToken ct);
}

public sealed class RobotsDisallowedException(string url) : Exception($"robots.txt disallows {url}");

/// <summary>
/// Single gateway for all requests to the site: robots.txt check, random delay, limited concurrency.
/// </summary>
public sealed class PoliteHttpClient : IPageFetcher, IDisposable
{
    public const string HttpClientName = "priceoye";

    private readonly IHttpClientFactory _factory;
    private readonly IHtmlRenderer _renderer;
    private readonly AppSettings _settings;
    private readonly ILogger<PoliteHttpClient> _log;
    private readonly SemaphoreSlim _gate;
    private readonly SemaphoreSlim _robotsLock = new(1, 1);
    private RobotsTxtPolicy? _robots;
    private int _delayMs;

    public PoliteHttpClient(IHttpClientFactory factory, IHtmlRenderer renderer, AppSettings settings, ILogger<PoliteHttpClient> log)
    {
        _factory = factory;
        _renderer = renderer;
        _settings = settings;
        _log = log;
        _delayMs = settings.DelayMs;
        _gate = new SemaphoreSlim(Math.Clamp(settings.MaxParallelRequests, 1, 3));
    }

    public int DelayMs
    {
        get => Volatile.Read(ref _delayMs);
        set => Volatile.Write(ref _delayMs, Math.Max(0, value));
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        await EnsureAllowedAsync(url, ct);
        await _gate.WaitAsync(ct);
        try
        {
            await PoliteDelayAsync(ct);
            _log.LogDebug("GET {Url}", url);
            var client = _factory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetRenderedAsync(string url, CancellationToken ct)
    {
        await EnsureAllowedAsync(url, ct);
        await _gate.WaitAsync(ct);
        try
        {
            await PoliteDelayAsync(ct);
            _log.LogDebug("RENDER {Url}", url);
            return await _renderer.RenderAsync(url, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsAllowedAsync(string url, CancellationToken ct)
    {
        var robots = await GetRobotsAsync(ct);
        return robots.IsAllowed(new Uri(url));
    }

    private async Task EnsureAllowedAsync(string url, CancellationToken ct)
    {
        if (!await IsAllowedAsync(url, ct))
            throw new RobotsDisallowedException(url);
    }

    /// <summary>Random delay between 2/3 and 5/3 of the base delay (1000–2500 ms for the default 1500 ms).</summary>
    private Task PoliteDelayAsync(CancellationToken ct)
    {
        var baseDelay = DelayMs;
        if (baseDelay <= 0) return Task.CompletedTask;
        var ms = Random.Shared.Next(baseDelay * 2 / 3, baseDelay * 5 / 3 + 1);
        return Task.Delay(ms, ct);
    }

    private async Task<RobotsTxtPolicy> GetRobotsAsync(CancellationToken ct)
    {
        if (_robots is not null) return _robots;
        await _robotsLock.WaitAsync(ct);
        try
        {
            if (_robots is not null) return _robots;
            var robotsUrl = new Uri(new Uri(_settings.BaseUrl), "/robots.txt");
            try
            {
                var client = _factory.CreateClient(HttpClientName);
                using var response = await client.GetAsync(robotsUrl, ct);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _robots = RobotsTxtPolicy.Parse(body, _settings.UserAgent);
                    _log.LogInformation("robots.txt loaded: {Count} rules apply to agent '{Agent}'",
                        _robots.RuleCount, RobotsTxtPolicy.ProductToken(_settings.UserAgent));
                }
                else if ((int)response.StatusCode < 500)
                {
                    // RFC 9309: a 4xx robots.txt means the site has no restrictions.
                    _log.LogWarning("robots.txt returned {Status}; treating site as unrestricted", (int)response.StatusCode);
                    _robots = RobotsTxtPolicy.AllowAll;
                }
                else
                {
                    // RFC 9309: a 5xx means "assume complete disallow" until robots.txt can be read.
                    throw new HttpRequestException($"robots.txt returned {(int)response.StatusCode}; refusing to crawl until it is readable");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not cached, so the next request tries again.
                _log.LogWarning("Could not read robots.txt: {Message}", ex.Message);
                throw;
            }
            return _robots;
        }
        finally
        {
            _robotsLock.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        _robotsLock.Dispose();
    }
}
