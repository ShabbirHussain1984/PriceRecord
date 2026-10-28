using PriceRecord.Config;
using PriceRecord.Data;
using PriceRecord.Models;
using PriceRecord.Services;
using Serilog.Events;

namespace PriceRecord.UI;

public partial class MainForm : Form
{
    private const int MaxLogLines = 3000;

    private readonly IScraperService _scraper;
    private readonly IProductRepository _repository;
    private readonly IExportService _export;
    private readonly AppSettings _settings;
    private readonly UiLogSink _logSink;
    private readonly Serilog.ILogger _log = Serilog.Log.ForContext<MainForm>();

    private readonly List<Product> _allProducts = new();
    private readonly Dictionary<string, Product> _byUrl = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortableBindingList<Product> _view = new();

    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _closeRequested;

    public MainForm(IScraperService scraper, IProductRepository repository, IExportService export, AppSettings settings, UiLogSink logSink)
    {
        _scraper = scraper;
        _repository = repository;
        _export = export;
        _settings = settings;
        _logSink = logSink;

        InitializeComponent();
        ConfigureGrid();
        ConfigureToolTips();
        _logSink.Emitted += OnLogEmitted;
    }

    // ---- Setup -------------------------------------------------------------------------------------

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        txtConnectionString.Text = _settings.ConnectionString;
        numDelay.Value = Math.Clamp(_settings.DelayMs, (int)numDelay.Minimum, (int)numDelay.Maximum);
        numMaxPages.Value = Math.Clamp(_settings.MaxPagesPerCategory, (int)numMaxPages.Minimum, (int)numMaxPages.Maximum);
        chkFetchDetails.Checked = _settings.FetchProductDetails;
        RefreshResumeCheckbox();

        _log.Information("Ready. Log file folder: {Folder}", Path.Combine(AppContext.BaseDirectory, _settings.LogDirectory));
        await LoadCategoriesAsync();
    }

    private void ConfigureGrid()
    {
        grdProducts.AutoGenerateColumns = false;
        grdProducts.DataSource = _view;

        AddColumn(nameof(Product.ProductId), "Id", 55);
        AddColumn(nameof(Product.ItemName), "Item Name", 240);
        AddColumn(nameof(Product.Category), "Category", 110);
        AddColumn(nameof(Product.OriginalPrice), "Original (PKR)", 100, "N0");
        AddColumn(nameof(Product.DiscountAmount), "Discount (PKR)", 100, "N0");
        AddColumn(nameof(Product.DiscountPercentage), "Discount %", 80, "0.00");
        AddColumn(nameof(Product.PriceAfterDiscount), "Price (PKR)", 100, "N0");
        AddColumn(nameof(Product.StockStatus), "Stock", 90);
        AddColumn(nameof(Product.ItemDescription), "Description", 300);
        AddColumn(nameof(Product.ProductUrl), "URL", 280);
        AddColumn(nameof(Product.ImageUrl), "Image", 200);
        AddColumn(nameof(Product.ScrapedAt), "Scraped At", 130, "yyyy-MM-dd HH:mm");
        grdProducts.DoubleBuffered(true);
    }

    private void AddColumn(string property, string header, int width, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = header,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic,
        };
        if (format is not null)
        {
            column.DefaultCellStyle.Format = format;
            if (format != "yyyy-MM-dd HH:mm")
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        grdProducts.Columns.Add(column);
    }

    private void ConfigureToolTips()
    {
        toolTip.SetToolTip(numDelay, "Base delay between requests. Each wait is randomised between 2/3 and 5/3 of this value.");
        toolTip.SetToolTip(chkFetchDetails, "Open every product page for its short description (one extra request per product).");
        toolTip.SetToolTip(chkResume, "Continue from the last completed category/page of a stopped run.");
        toolTip.SetToolTip(numMaxPages, "0 = all pages. Each listing page has about 36 products.");
    }

    // ---- Categories --------------------------------------------------------------------------------

    private async void btnLoadCategories_Click(object? sender, EventArgs e) => await LoadCategoriesAsync();

    private async Task LoadCategoriesAsync()
    {
        btnLoadCategories.Enabled = false;
        lblStatus.Text = "Loading categories…";
        try
        {
            var previouslyChecked = lstCategories.CheckedItems.Cast<CategoryInfo>().Select(c => c.Slug).ToHashSet();
            var categories = await _scraper.GetCategoriesAsync(CancellationToken.None);

            lstCategories.BeginUpdate();
            lstCategories.Items.Clear();
            foreach (var category in categories)
                lstCategories.Items.Add(category, previouslyChecked.Contains(category.Slug));
            lstCategories.EndUpdate();

            lblStatus.Text = $"{categories.Count} categories loaded";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not load categories");
            lblStatus.Text = "Could not load categories — see log";
        }
        finally
        {
            btnLoadCategories.Enabled = !_running;
        }
    }

    private void chkSelectAll_CheckedChanged(object? sender, EventArgs e)
    {
        for (var i = 0; i < lstCategories.Items.Count; i++)
            lstCategories.SetItemChecked(i, chkSelectAll.Checked);
    }

    // ---- Database ------------------------------------------------------------------------------------

    private async void btnTestConnection_Click(object? sender, EventArgs e) => await TestConnectionAsync();

    private async Task<bool> TestConnectionAsync()
    {
        btnTestConnection.Enabled = false;
        try
        {
            _repository.ConnectionString = txtConnectionString.Text.Trim();
            var result = await _repository.TestConnectionAsync(CancellationToken.None);
            if (!result.Success)
                _log.Error("Connection failed: {Message}", result.Message);
            else if (!result.SchemaPresent)
                _log.Warning("{Message}", result.Message);
            else
                _log.Information("{Message}", result.Message);
            return result.Success && result.SchemaPresent;
        }
        finally
        {
            btnTestConnection.Enabled = !_running;
        }
    }

    // ---- Run ---------------------------------------------------------------------------------------------

    private async void btnStart_Click(object? sender, EventArgs e)
    {
        var categories = lstCategories.CheckedItems.Cast<CategoryInfo>().ToList();
        if (categories.Count == 0)
        {
            MessageBox.Show(this, "Tick at least one category.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!await TestConnectionAsync())
        {
            MessageBox.Show(this, "The database is not ready. Check the connection string and run CreateDatabase.sql (details in the log).",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var options = new ScrapeOptions
        {
            Categories = categories,
            DelayMs = (int)numDelay.Value,
            MaxPagesPerCategory = (int)numMaxPages.Value,
            FetchProductDetails = chkFetchDetails.Checked,
            Resume = chkResume.Checked,
        };

        _cts = new CancellationTokenSource();
        SetRunning(true);
        progressBar.Value = 0;
        var progress = new Progress<ScrapeProgress>(OnProgress);

        try
        {
            var summary = await Task.Run(() => _scraper.RunAsync(options, progress, _cts.Token));
            lblStatus.Text = summary.Cancelled
                ? $"Stopped — {summary.ProductsSaved} products saved"
                : $"Finished — {summary.ProductsSaved} products saved, {summary.ProductsFailed} failed, {summary.PagesFailed} pages failed";
            if (!summary.Cancelled) progressBar.Value = 100;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Run aborted");
            lblStatus.Text = "Run aborted — see log";
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetRunning(false);
            RefreshResumeCheckbox();
        }

        if (_closeRequested) Close();
    }

    private void btnStop_Click(object? sender, EventArgs e)
    {
        btnStop.Enabled = false;
        lblStatus.Text = "Stopping after the current request…";
        _cts?.Cancel();
    }

    private void numDelay_ValueChanged(object? sender, EventArgs e)
    {
        if (_running) _scraper.DelayMs = (int)numDelay.Value;
    }

    private void OnProgress(ScrapeProgress p)
    {
        progressBar.Value = Math.Clamp(p.Percent, 0, 100);
        lblStatus.Text = p.StatusText;
        if (p.NewProducts.Count > 0) AddProducts(p.NewProducts);
    }

    private void SetRunning(bool running)
    {
        _running = running;
        btnStart.Enabled = !running;
        btnStop.Enabled = running;
        btnTestConnection.Enabled = !running;
        btnLoadCategories.Enabled = !running;
        lstCategories.Enabled = !running;
        chkSelectAll.Enabled = !running;
        chkResume.Enabled = !running && _scraper.HasResumeState;
        chkFetchDetails.Enabled = !running;
        numMaxPages.Enabled = !running;
        txtConnectionString.ReadOnly = running;
    }

    private void RefreshResumeCheckbox()
    {
        var hasState = _scraper.HasResumeState;
        chkResume.Enabled = hasState && !_running;
        chkResume.Checked = hasState;
    }

    // ---- Grid --------------------------------------------------------------------------------------------

    private void AddProducts(IReadOnlyList<Product> products)
    {
        var filter = txtFilter.Text.Trim();
        foreach (var p in products)
        {
            if (_byUrl.TryGetValue(p.ProductUrl, out var existing))
            {
                _allProducts[_allProducts.IndexOf(existing)] = p;
                var viewIndex = _view.IndexOf(existing);
                if (viewIndex >= 0) _view[viewIndex] = p;
            }
            else
            {
                _allProducts.Add(p);
                if (Matches(p, filter)) _view.Add(p);
            }
            _byUrl[p.ProductUrl] = p;
        }
        lblRowCount.Text = $"{_view.Count:N0} of {_allProducts.Count:N0} rows";
    }

    private void txtFilter_TextChanged(object? sender, EventArgs e)
    {
        filterTimer.Stop();
        filterTimer.Start();
    }

    private void filterTimer_Tick(object? sender, EventArgs e)
    {
        filterTimer.Stop();
        var filter = txtFilter.Text.Trim();
        _view.ResetItems(_allProducts.Where(p => Matches(p, filter)));
        lblRowCount.Text = $"{_view.Count:N0} of {_allProducts.Count:N0} rows";
    }

    private static bool Matches(Product p, string filter)
        => filter.Length == 0 || p.ItemName.Contains(filter, StringComparison.OrdinalIgnoreCase);

    // ---- Export --------------------------------------------------------------------------------------------

    private async void btnExport_Click(object? sender, EventArgs e)
    {
        if (_view.Count == 0)
        {
            MessageBox.Show(this, "There are no rows to export yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv|Excel workbook (*.xlsx)|*.xlsx",
            FileName = $"priceoye-products-{DateTime.Now:yyyyMMdd-HHmm}",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var rows = _view.ToList();
        var path = dialog.FileName;
        btnExport.Enabled = false;
        try
        {
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                await Task.Run(() => _export.ExportExcel(rows, path));
            else
                await _export.ExportCsvAsync(rows, path, CancellationToken.None);
            _log.Information("Exported {Count} rows to {Path}", rows.Count, path);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Export failed");
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnExport.Enabled = true;
        }
    }

    // ---- Log box ---------------------------------------------------------------------------------------------

    private void OnLogEmitted(LogEvent e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        var text = $"{e.Timestamp:HH:mm:ss} [{Abbreviation(e.Level)}] {e.RenderMessage()}";
        if (e.Exception is not null) text += $" — {e.Exception.GetType().Name}: {e.Exception.Message}";
        try
        {
            BeginInvoke(() => AppendLog(text, e.Level));
        }
        catch (InvalidOperationException)
        {
            // form is closing
        }
    }

    private void AppendLog(string text, LogEventLevel level)
    {
        if (rtbLog.IsDisposed) return;
        if (rtbLog.Lines.Length > MaxLogLines)
        {
            rtbLog.ReadOnly = false;
            rtbLog.Select(0, rtbLog.GetFirstCharIndexFromLine(MaxLogLines / 5));
            rtbLog.SelectedText = string.Empty;
            rtbLog.ReadOnly = true;
        }

        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = level switch
        {
            LogEventLevel.Error or LogEventLevel.Fatal => Color.FromArgb(255, 110, 110),
            LogEventLevel.Warning => Color.FromArgb(255, 200, 90),
            LogEventLevel.Debug or LogEventLevel.Verbose => Color.Gray,
            _ => Color.Gainsboro,
        };
        rtbLog.AppendText(text + Environment.NewLine);
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();
    }

    private static string Abbreviation(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        _ => "FTL",
    };

    private void DetachLogSink() => _logSink.Emitted -= OnLogEmitted;

    // ---- Closing -----------------------------------------------------------------------------------------------

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_running) return;
        // Let the run stop cleanly (current page saved, resume state written), then close.
        e.Cancel = true;
        _closeRequested = true;
        btnStop_Click(sender, EventArgs.Empty);
    }
}

internal static class ControlExtensions
{
    /// <summary>Reduces flicker while rows are added during a run.</summary>
    public static void DoubleBuffered(this DataGridView grid, bool enabled)
        => typeof(DataGridView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(grid, enabled);
}
