using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using PriceRecord.Models;

namespace PriceRecord.Services;

public interface IExportService
{
    Task ExportCsvAsync(IEnumerable<Product> products, string path, CancellationToken ct);
    void ExportExcel(IEnumerable<Product> products, string path);
}

public sealed class ExportService : IExportService
{
    private static readonly string[] Headers =
    [
        "ProductId", "ItemName", "ItemDescription", "Category", "OriginalPrice", "DiscountAmount",
        "DiscountPercentage", "PriceAfterDiscount", "StockStatus", "ProductUrl", "ImageUrl", "ScrapedAt",
    ];

    public async Task ExportCsvAsync(IEnumerable<Product> products, string path, CancellationToken ct)
    {
        // UTF-8 with BOM so Excel shows Urdu/special characters correctly.
        await using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        await writer.WriteLineAsync(string.Join(",", Headers));
        foreach (var p in products)
        {
            ct.ThrowIfCancellationRequested();
            var fields = new[]
            {
                p.ProductId.ToString(CultureInfo.InvariantCulture), p.ItemName, p.ItemDescription, p.Category,
                Num(p.OriginalPrice), Num(p.DiscountAmount), Num(p.DiscountPercentage), Num(p.PriceAfterDiscount),
                p.StockStatus, p.ProductUrl, p.ImageUrl, p.ScrapedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            };
            await writer.WriteLineAsync(string.Join(",", fields.Select(Escape)));
        }
    }

    public void ExportExcel(IEnumerable<Product> products, string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Products");
        for (var c = 0; c < Headers.Length; c++)
            sheet.Cell(1, c + 1).Value = Headers[c];

        var row = 2;
        foreach (var p in products)
        {
            sheet.Cell(row, 1).Value = p.ProductId;
            sheet.Cell(row, 2).Value = p.ItemName;
            sheet.Cell(row, 3).Value = p.ItemDescription;
            sheet.Cell(row, 4).Value = p.Category;
            sheet.Cell(row, 5).Value = p.OriginalPrice;
            sheet.Cell(row, 6).Value = p.DiscountAmount;
            sheet.Cell(row, 7).Value = p.DiscountPercentage;
            sheet.Cell(row, 8).Value = p.PriceAfterDiscount;
            sheet.Cell(row, 9).Value = p.StockStatus;
            sheet.Cell(row, 10).Value = p.ProductUrl;
            sheet.Cell(row, 11).Value = p.ImageUrl;
            sheet.Cell(row, 12).Value = p.ScrapedAt;
            row++;
        }

        var header = sheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Columns(5, 8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(12).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Min(row, 500));
        sheet.Column(3).Width = 60;
        workbook.SaveAs(path);
    }

    private static string Num(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
