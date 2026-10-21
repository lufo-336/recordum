using System.Text.Json.Serialization;
using Recordum.Core.Enums;

namespace Recordum.Plugins.Contracts.Export;

/// <summary>Platform-neutral, UI-free description of what to export.</summary>
public sealed record ExportModel(
    ExportSubject Subject,
    string Title,
    DateOnly From,
    DateOnly To,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExportHabitModel> Habits);

public sealed record ExportHabitModel(
    Guid HabitNodeId,
    string Name,
    string HabitType,
    string? ColorHex,
    ExportSummary Summary,
    IReadOnlyList<ExportCompletionRow> Completions,
    IReadOnlyList<ExportNoteRow> Notes,
    IReadOnlyList<ExportHeatmapCell> Heatmap)
{
    /// <summary>Resistance (abstinence) attempts for this habit. Empty for every other habit type.</summary>
    public IReadOnlyList<ExportPeriodRow> Periods { get; init; } = [];
}

public sealed record ExportSummary(
    int CurrentStreak,
    int LongestStreak,
    int TotalCompletions,
    decimal? GoalActual,
    decimal? GoalTarget)
{
    /// <summary>Unit <see cref="GoalActual"/>/<see cref="GoalTarget"/> are expressed in, resolved the
    /// same way as <see cref="ExportCompletionRow.Unit"/>. Null for a goal that counts completions —
    /// there is no unit — and for a metric that declares none.</summary>
    public string? GoalUnit { get; init; }
}

public sealed record ExportCompletionRow(DateTimeOffset CompletedAt, decimal? Value, string? Metric, bool Retroactive)
{
    /// <summary>Unit code the <see cref="Value"/> is actually expressed in — the metric's own unit
    /// once a duration has been converted into it, or "seconds" for a duration the metric gave no
    /// time unit to convert into. Null when the metric declares no unit. Without it a duration
    /// exports as a bare number that means hours in one file and seconds in the next.</summary>
    public string? Unit { get; init; }
}

public sealed record ExportNoteRow(DateTimeOffset UpdatedAt, string BodyMd)
{
    /// <summary>Photos/files attached to this note. Empty for notes without attachments.</summary>
    public IReadOnlyList<ExportAttachmentRef> Attachments { get; init; } = [];
}

/// <summary>One photo/file attached to a note, described for export purposes.</summary>
public sealed record ExportAttachmentRef(string Filename, string MimeType, string RelativePath)
{
    /// <summary>The photo bytes, read from the database at model-build time so HTML/PDF formatters can
    /// embed them without depending on <c>IAttachmentService</c>. Never serialized: it would bloat (and
    /// duplicate) the JSON export. Null when the row has no content (legacy, missing file).</summary>
    [JsonIgnore]
    public byte[]? Content { get; init; }
}

/// <summary>One abstinence attempt: start, optional end (open = still running), elapsed span.</summary>
public sealed record ExportPeriodRow(DateTimeOffset StartedAt, DateTimeOffset? EndedAt, TimeSpan Duration, bool IsOpen);

/// <summary>One year-heatmap cell: column (week 0..52), row (weekday 0=Mon..6=Sun), intensity step 0..4.</summary>
public readonly record struct ExportHeatmapCell(int Week, int Weekday, int Step);
