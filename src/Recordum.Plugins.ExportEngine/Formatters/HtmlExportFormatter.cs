using System.Globalization;
using System.Net;
using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class HtmlExportFormatter : ExportFormatterBase
{
    public override ExportFormat Format => ExportFormat.Html;

    public override byte[] Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><title>")
          .Append(WebUtility.HtmlEncode(model.Title)).Append("</title>")
          .Append("<style>body{font-family:system-ui,sans-serif;margin:2rem;}table{border-collapse:collapse}td,th{border:1px solid #ddd;padding:4px 8px}</style>")
          .Append("</head><body>");
        sb.Append("<h1>").Append(WebUtility.HtmlEncode(model.Title)).Append("</h1>");
        sb.Append("<p><em>Generated ").Append(model.GeneratedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)).Append("</em></p>");

        foreach (var h in model.Habits)
        {
            if (model.Subject == ExportSubject.Library) sb.Append("<h2>").Append(WebUtility.HtmlEncode(h.Name)).Append("</h2>");
            sb.Append("<ul><li>Current streak: ").Append(h.Summary.CurrentStreak)
              .Append("</li><li>Longest streak: ").Append(h.Summary.LongestStreak)
              .Append("</li><li>Total completions: ").Append(h.Summary.TotalCompletions).Append("</li></ul>");

            if (h.Heatmap.Count > 0)
            {
                var png = Convert.ToBase64String(HeatmapImageRenderer.RenderPng(h.Heatmap));
                sb.Append("<img alt=\"heatmap\" src=\"data:image/png;base64,").Append(png).Append("\"/>");
            }

            if (h.Completions.Count > 0)
            {
                sb.Append("<table><tr><th>Date</th><th>Value</th><th>Metric</th></tr>");
                foreach (var c in h.Completions)
                    sb.Append("<tr><td>").Append(c.CompletedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                      .Append("</td><td>").Append(WebUtility.HtmlEncode(c.Value?.ToString(CultureInfo.InvariantCulture) ?? ""))
                      .Append("</td><td>").Append(WebUtility.HtmlEncode(c.Metric ?? "")).Append("</td></tr>");
                sb.Append("</table>");
            }

            if (h.Notes.Count > 0)
            {
                sb.Append("<h3>Notes</h3><ul>");
                foreach (var n in h.Notes)
                    sb.Append("<li>").Append(WebUtility.HtmlEncode(n.BodyMd)).Append("</li>");
                sb.Append("</ul>");
            }
        }
        sb.Append("</body></html>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
