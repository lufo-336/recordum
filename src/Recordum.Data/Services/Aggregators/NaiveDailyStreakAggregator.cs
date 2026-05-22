using Recordum.Core.Abstractions.Services;

namespace Recordum.Data.Services.Aggregators;

public sealed class NaiveDailyStreakAggregator(ICompletionService completions) : IStreakAggregator
{
    public async ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var cells = await completions.GetHeatmapAsync(habitNodeId, fromDate, toDate, ct).ConfigureAwait(false);
        var active = cells.Where(c => c.Count > 0).Select(c => c.Date).ToHashSet();
        if (active.Count == 0) return Array.Empty<StreakRun>();

        var ordered = active.OrderBy(d => d.DayNumber).ToList();
        var runs = new List<StreakRun>();
        var runStart = ordered[0];
        var runEnd = ordered[0];
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].DayNumber == runEnd.DayNumber + 1)
            {
                runEnd = ordered[i];
            }
            else
            {
                runs.Add(new StreakRun(runStart, runEnd, runEnd.DayNumber - runStart.DayNumber + 1));
                runStart = ordered[i];
                runEnd = ordered[i];
            }
        }
        runs.Add(new StreakRun(runStart, runEnd, runEnd.DayNumber - runStart.DayNumber + 1));
        return runs;
    }
}
