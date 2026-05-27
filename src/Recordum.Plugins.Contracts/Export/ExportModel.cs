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
    IReadOnlyList<ExportHeatmapCell> Heatmap);

public sealed record ExportSummary(
    int CurrentStreak,
    int LongestStreak,
    int TotalCompletions,
    decimal? GoalActual,
    decimal? GoalTarget);

public sealed record ExportCompletionRow(DateTimeOffset CompletedAt, decimal? Value, string? Metric, bool Retroactive);

public sealed record ExportNoteRow(DateTimeOffset UpdatedAt, string BodyMd);

/// <summary>One year-heatmap cell: column (week 0..52), row (weekday 0=Mon..6=Sun), intensity step 0..4.</summary>
public readonly record struct ExportHeatmapCell(int Week, int Weekday, int Step);
