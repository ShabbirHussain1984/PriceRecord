using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using PriceRecord.Config;
using PriceRecord.Data;
using PriceRecord.Services;
using PriceRecord.UI;
using Serilog;

namespace PriceRecord
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var settings = configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
            var selectors = configuration.GetSection("Selectors").Get<Selectors>() ?? new Selectors();

            var uiSink = new UiLogSink();
            var logDirectory = Path.IsPathRooted(settings.LogDirectory)
                ? settings.LogDirectory
                : Path.Combine(AppContext.BaseDirectory, settings.LogDirectory);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("Polly", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File(Path.Combine(logDirectory, "pricerecord-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Sink(uiSink, Serilog.Events.LogEventLevel.Information)
                .CreateLogger();

            Application.ThreadException += (_, e) => Log.Error(e.Exception, "Unhandled UI exception");
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");

            try
            {
                using var provider = ConfigureServices(settings, selectors, uiSink).BuildServiceProvider();
                Application.Run(provider.GetRequiredService<MainForm>());
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static ServiceCollection ConfigureServices(AppSettings settings, Selectors selectors, UiLogSink uiSink)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(dispose: false));

            services.AddSingleton(settings);
            services.AddSingleton(selectors);
            services.AddSingleton(uiSink);

            services.AddHttpClient(PoliteHttpClient.HttpClientName, client =>
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
                    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                    // Timeouts are per attempt in the resilience pipeline; a long 429 pause must not be cut short here.
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                })
                .AddResilienceHandler("polite-retry", (pipeline, context) =>
                {
                    var log = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("HttpRetry");
                    pipeline.AddRetry(new HttpRetryStrategyOptions
                    {
                        // Default ShouldHandle covers HttpRequestException, timeouts, 408, 429 and 5xx.
                        MaxRetryAttempts = Math.Max(0, settings.MaxRetries),
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(2),
                        ShouldRetryAfterHeader = false,
                        DelayGenerator = args =>
                        {
                            var response = args.Outcome.Result;
                            if (response?.StatusCode != HttpStatusCode.TooManyRequests)
                                return ValueTask.FromResult<TimeSpan?>(null); // exponential backoff
                            // 429: pause much longer, growing with each attempt, or as long as the server asks.
                            var pause = TimeSpan.FromSeconds(settings.TooManyRequestsPauseSeconds * (args.AttemptNumber + 1));
                            var retryAfter = response.Headers.RetryAfter;
                            var requested = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow);
                            return ValueTask.FromResult<TimeSpan?>(requested > pause ? requested : pause);
                        },
                        OnRetry = args =>
                        {
                            var reason = args.Outcome.Result is { } r ? $"HTTP {(int)r.StatusCode}" : args.Outcome.Exception?.Message;
                            log.LogWarning("Retry {Attempt}/{Max} in {Delay:0.#}s after {Reason}",
                                args.AttemptNumber + 1, settings.MaxRetries, args.RetryDelay.TotalSeconds, reason);
                            return ValueTask.CompletedTask;
                        },
                    });
                    pipeline.AddTimeout(TimeSpan.FromSeconds(Math.Max(5, settings.RequestTimeoutSeconds)));
                });

            services.AddSingleton<IHtmlRenderer, PlaywrightRenderer>();
            services.AddSingleton<IPageFetcher, PoliteHttpClient>();
            services.AddSingleton(sp => new PriceOyePageParser(sp.GetRequiredService<Selectors>(), settings.BaseUrl));
            services.AddSingleton<IProductRepository, SqlProductRepository>();
            services.AddSingleton<IResumeStore, JsonResumeStore>();
            services.AddSingleton<IScraperService, PriceOyeScraper>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddTransient<MainForm>();
            return services;
        }
    }
}
