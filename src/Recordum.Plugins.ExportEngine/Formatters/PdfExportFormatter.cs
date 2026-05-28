using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class PdfExportFormatter : ExportFormatterBase
{
    public override ExportFormat Format => ExportFormat.Pdf;

    public override byte[] Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.Header().Text(model.Title).FontSize(20).Bold();
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Generated {model.GeneratedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);

                    foreach (var h in model.Habits)
                    {
                        if (model.Subject == ExportSubject.Library)
                            col.Item().PaddingTop(8).Text(h.Name).FontSize(15).SemiBold();
                        col.Item().Text($"Current streak: {h.Summary.CurrentStreak}   Longest: {h.Summary.LongestStreak}   Total: {h.Summary.TotalCompletions}");

                        if (h.Heatmap.Count > 0)
                            col.Item().Image(HeatmapImageRenderer.RenderPng(h.Heatmap)).FitWidth();

                        if (h.Completions.Count > 0)
                        {
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                t.Header(hd =>
                                {
                                    hd.Cell().Text("Date").SemiBold();
                                    hd.Cell().Text("Value").SemiBold();
                                    hd.Cell().Text("Metric").SemiBold();
                                });
                                foreach (var c in h.Completions)
                                {
                                    t.Cell().Text(c.CompletedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                                    t.Cell().Text(c.Value?.ToString(CultureInfo.InvariantCulture) ?? "");
                                    t.Cell().Text(c.Metric ?? "");
                                }
                            });
                        }
                    }
                });
                page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
            });
        }).GeneratePdf();
    }
}
