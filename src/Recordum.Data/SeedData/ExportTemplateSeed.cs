using Recordum.Core.Models;

namespace Recordum.Data.SeedData;

/// <summary>
/// Official seed data for ExportTemplate. GUIDs and LayoutJson are stable constants required by EF Core HasData.
/// Timestamps are literal constants — TimestampInterceptor does not run during HasData INSERT generation.
/// </summary>
public static class ExportTemplateSeed
{
    private static readonly DateTime SeedDate = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
    private static readonly long SeedDateMs = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    // Pre-computed JSON layout descriptors as string literals for HasData determinism.

    private const string GlobalJournalLayout =
        "{\"blocks\":[{\"type\":\"title\",\"text\":\"Global Journal\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"journal\"},{\"type\":\"completion_timeline\",\"include_notes\":true},{\"type\":\"attachment_gallery\"}]}";

    private const string FitnessReportLayout =
        "{\"blocks\":[{\"type\":\"title\",\"text\":\"Fitness Report\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"category:Fitness\"},{\"type\":\"metric_chart\",\"chart_type\":\"line\"},{\"type\":\"streak_summary\"},{\"type\":\"goal_progress\"}]}";

    private const string HealthReportLayout =
        "{\"blocks\":[{\"type\":\"title\",\"text\":\"Health Report\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"category:Health\"},{\"type\":\"completion_heatmap\"},{\"type\":\"metric_chart\",\"chart_type\":\"bar\"},{\"type\":\"note_list\"}]}";

    private const string MinimalSummaryLayout =
        "{\"blocks\":[{\"type\":\"title\",\"text\":\"Summary\"},{\"type\":\"date_range_picker\"},{\"type\":\"habit_list\",\"filter\":\"all\",\"show_streaks\":true},{\"type\":\"completion_timeline\"}]}";

    public static IReadOnlyList<ExportTemplate> All { get; } =
    [
        new ExportTemplate
        {
            Id = Guid.Parse("00000004-0000-0000-0000-000000000001"),
            Name = "Global Journal",
            Description = "Full journal export covering all journal-type habits across a chosen date range.",
            LayoutJson = GlobalJournalLayout,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new ExportTemplate
        {
            Id = Guid.Parse("00000004-0000-0000-0000-000000000002"),
            Name = "Fitness Report",
            Description = "Performance report for Fitness category habits with metric charts and streak summaries.",
            LayoutJson = FitnessReportLayout,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new ExportTemplate
        {
            Id = Guid.Parse("00000004-0000-0000-0000-000000000003"),
            Name = "Health Report",
            Description = "Clinical-style report for Health category habits, suitable for sharing with a practitioner.",
            LayoutJson = HealthReportLayout,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new ExportTemplate
        {
            Id = Guid.Parse("00000004-0000-0000-0000-000000000004"),
            Name = "Minimal Summary",
            Description = "Compact overview of all habits with streak indicators for a chosen date range.",
            LayoutJson = MinimalSummaryLayout,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
    ];
}
