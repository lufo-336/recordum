using System.Globalization;
using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class MarkdownExportFormatter : ExportFormatterBase
{
    public override ExportFormat Format => ExportFormat.Markdown;

    public override byte[] Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var sb = new StringBuilder();
        sb.Append("# ").Append(model.Title).Append("\n\n");
        sb.Append("_Generated ").Append(model.GeneratedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)).Append("_\n\n");

        foreach (var h in model.Habits)
        {
            if (model.Subject == ExportSubject.Library) sb.Append("## ").Append(h.Name).Append("\n\n");
            sb.Append("- Current streak: ").Append(h.Summary.CurrentStreak).Append('\n');
            sb.Append("- Longest streak: ").Append(h.Summary.LongestStreak).Append('\n');
            sb.Append("- Total completions: ").Append(h.Summary.TotalCompletions).Append("\n\n");

            if (h.Completions.Count > 0)
            {
                sb.Append("| Date | Value | Metric |\n|---|---|---|\n");
                foreach (var c in h.Completions)
                    sb.Append("| ").Append(c.CompletedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                      .Append(" | ").Append(c.Value?.ToString(CultureInfo.InvariantCulture) ?? "")
                      .Append(" | ").Append(c.Metric ?? "").Append(" |\n");
                sb.Append('\n');
            }

            if (h.Notes.Count > 0)
            {
                sb.Append("### Notes\n\n");
                foreach (var n in h.Notes)
                    sb.Append("- ").Append(n.UpdatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(": ")
                      .Append(n.BodyMd.ReplaceLineEndings(" ")).Append('\n');
                sb.Append('\n');
            }
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
