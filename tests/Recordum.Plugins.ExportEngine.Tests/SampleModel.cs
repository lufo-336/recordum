using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Export;

namespace Recordum.Plugins.ExportEngine.Tests;

internal static class SampleModel
{
    public static ExportModel One() => new(
        ExportSubject.SingleHabit, "Run",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 25),
        new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero),
        [
            new ExportHabitModel(
                Guid.Parse("11111111-1111-1111-1111-111111111111"), "Run", "Action", "#112233",
                new ExportSummary(3, 7, 12, 12m, 20m),
                [new ExportCompletionRow(new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero), 5m, "km", false)],
                [new ExportNoteRow(new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero), "felt good")],
                [new ExportHeatmapCell(10, 2, 4), new ExportHeatmapCell(11, 0, 2)]),
        ]);

    /// <summary>A Resistance (abstinence) habit: one closed period + one still-open period, no completions.</summary>
    public static ExportModel WithAbstinencePeriods() => new(
        ExportSubject.SingleHabit, "Quit smoking",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 3),
        new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
        [
            new ExportHabitModel(
                Guid.Parse("22222222-2222-2222-2222-222222222222"), "Quit smoking", "Resistance", "#aa3344",
                new ExportSummary(1, 18, 0, null, null),
                [],
                [],
                [])
            {
                Periods =
                [
                    new ExportPeriodRow(
                        new DateTimeOffset(2026, 6, 13, 9, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 7, 1, 19, 0, 0, TimeSpan.Zero),
                        new TimeSpan(18, 10, 0, 0),
                        false),
                    new ExportPeriodRow(
                        new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero),
                        null,
                        new TimeSpan(0, 4, 30, 0),
                        true),
                ],
            },
        ]);
}
