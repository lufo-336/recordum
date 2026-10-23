using Recordum.Core.Enums;
using Recordum.Core.Text;
using Recordum.Plugins.Contracts.Export;
using Recordum.Plugins.Contracts.ExtensionPoints;

namespace Recordum.Plugins.ExportEngine;

public abstract class ExportFormatterBase : IExportFormatter
{
    public const string EnginePluginId = "io.recordum.export-engine";

    public ExtensionPointId PointId => WellKnownExtensionPoints.ExportFormat;
    public string PluginId => EnginePluginId;

    public abstract ExportFormat Format { get; }
    public bool CanFormat(ExportFormat format) => format == Format;
    public abstract ExportOutput Write(ExportModel model);

    /// <summary>Formats an abstinence period's elapsed span as <c>d.hh:mm:ss</c>, invariant.</summary>
    protected static string FormatPeriodDuration(TimeSpan t)
        => t.ToString(@"d\.hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A completion's value for the human-readable formats: the number followed by the unit
    /// it is expressed in ("7.5 hours"), or the bare number when the metric declares none. Invariant,
    /// like every other value in an export file. The machine formats (CSV, JSON) keep the number and
    /// the unit apart instead, so a consumer never has to parse them back out of one string.</summary>
    /// <summary>"12 / 20", or "7.5 / 10 hours" when the goal tracks a metric that has a unit — null
    /// when the habit has no active goal, so callers can drop the line entirely rather than print an
    /// empty one.</summary>
    protected static string? FormatGoal(ExportSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.GoalTarget is not { } target) return null;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var pair = (summary.GoalActual ?? 0m).ToString(culture) + " / " + target.ToString(culture);
        return string.IsNullOrEmpty(summary.GoalUnit) ? pair : pair + " " + summary.GoalUnit;
    }

    protected static string FormatValue(ExportCompletionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Value is not { } v) return "";
        var number = v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(row.Unit) ? number : number + " " + row.Unit;
    }

    /// <summary>Walks a note body: plain stretches go through <paramref name="text"/>, each <c>photo:</c> reference
    /// through <paramref name="photo"/> (null attachment = the id matches none of the note's attachments).</summary>
    protected static string RenderBody(
        string body, IReadOnlyList<ExportAttachmentRef> attachments,
        Func<string, string> text, Func<ExportAttachmentRef?, string> photo)
    {
        var sb = new System.Text.StringBuilder();
        var pos = 0;
        foreach (var r in PhotoRefs.Find(body))
        {
            sb.Append(text(body[pos..r.Start])).Append(photo(attachments.FirstOrDefault(a => a.Id == r.Id)));
            pos = r.Start + r.Length;
        }
        return sb.Append(text(body[pos..])).ToString();
    }

    /// <summary>Attachments with bytes the body does not mention: formatters append them after the text.
    /// One without bytes and without a reference has nothing to show and is skipped.</summary>
    protected static IEnumerable<ExportAttachmentRef> Unreferenced(string body, IReadOnlyList<ExportAttachmentRef> attachments)
    {
        var ids = PhotoRefs.Find(body).Select(r => r.Id).ToHashSet();
        return attachments.Where(a => !ids.Contains(a.Id) && a.Content is { Length: > 0 });
    }

    /// <summary>Same note text with every photo reference removed (for formats that cannot place the image).</summary>
    public static string StripPhotoRefs(string body) => PhotoRefs.Replace(body, _ => "");

    protected static string MissingMarker(string? filename)
        => "[" + (filename ?? "photo").Replace("]", "", StringComparison.Ordinal) + " \u2014 missing]";
}
