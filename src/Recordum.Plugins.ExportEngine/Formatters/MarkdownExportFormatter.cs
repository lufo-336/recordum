using System.Globalization;
using System.Text;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Formatters;

public sealed class MarkdownExportFormatter : ExportFormatterBase
{
    public override ExportFormat Format => ExportFormat.Markdown;

    public override ExportOutput Write(ExportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var sb = new StringBuilder();
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        sb.Append("# ").Append(model.Title).Append("\n\n");
        sb.Append("_Generated ").Append(model.GeneratedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)).Append("_\n\n");

        foreach (var h in model.Habits)
        {
            if (model.Subject == ExportSubject.Library) sb.Append("## ").Append(h.Name).Append("\n\n");
            sb.Append("- Current streak: ").Append(h.Summary.CurrentStreak).Append('\n');
            sb.Append("- Longest streak: ").Append(h.Summary.LongestStreak).Append('\n');
            sb.Append("- Total completions: ").Append(h.Summary.TotalCompletions).Append('\n');
            if (FormatGoal(h.Summary) is { } goal) sb.Append("- Goal: ").Append(goal).Append('\n');
            sb.Append('\n');

            if (h.Completions.Count > 0)
            {
                sb.Append("| Date | Value | Metric |\n|---|---|---|\n");
                foreach (var c in h.Completions)
                    sb.Append("| ").Append(c.CompletedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                      .Append(" | ").Append(FormatValue(c))
                      .Append(" | ").Append(c.Metric ?? "").Append(" |\n");
                sb.Append('\n');
            }

            if (h.Periods.Count > 0)
            {
                sb.Append("| Started | Ended | Duration | Outcome |\n|---|---|---|---|\n");
                foreach (var p in h.Periods)
                    sb.Append("| ").Append(p.StartedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                      .Append(" | ").Append(p.EndedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "")
                      .Append(" | ").Append(FormatPeriodDuration(p.Duration))
                      .Append(" | ").Append(p.IsOpen ? "open" : "relapsed").Append(" |\n");
                sb.Append('\n');
            }

            if (h.Notes.Count > 0)
            {
                sb.Append("### Notes\n\n");
                foreach (var n in h.Notes)
                {
                    sb.Append("#### ").Append(n.UpdatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append("\n\n");
                    sb.Append(RenderBody(n.BodyMd.TrimEnd(), n.Attachments, t => t, a => Photo(a, files))).Append("\n\n");
                    foreach (var att in Unreferenced(n.BodyMd, n.Attachments))
                        sb.Append(Photo(att, files)).Append("\n\n");
                }
                sb.Append('\n');
            }
        }
        return new ExportOutput(Encoding.UTF8.GetBytes(sb.ToString()), [.. files.Select(f => new ExportFile(f.Key, f.Value))]);
    }

    /// <summary>The image link, registering the file for the host to write; a photo with no bytes (or an id the
    /// note does not have) becomes a visible marker, never an empty file or a broken link.</summary>
    private static string Photo(ExportAttachmentRef? att, Dictionary<string, byte[]> files)
    {
        if (att?.Content is not { Length: > 0 } bytes) return MissingMarker(att?.Filename);
        files[att.RelativePath] = bytes;
        return "![" + att.Filename.Replace("]", "", StringComparison.Ordinal) + "](" + att.RelativePath + ")";
    }
}
