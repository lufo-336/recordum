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
}
