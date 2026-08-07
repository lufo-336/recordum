using Recordum.Core.Enums;
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
    public abstract byte[] Write(ExportModel model);

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
}
