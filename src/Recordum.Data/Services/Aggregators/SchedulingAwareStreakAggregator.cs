using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Data.Services.Aggregators;

public sealed class SchedulingAwareStreakAggregator(
    IHabitNodeService nodes,
    IHabitConfigService configs,
    ICompletionService completions,
    IStreakPauseService pauses,
    IScheduleEvaluator scheduler,
    IClock clock) : IStreakAggregator
{
    public async ValueTask<IReadOnlyList<StreakRun>> GetHistoryAsync(
        Guid habitNodeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        if (fromDate > toDate)
            throw new ArgumentException("fromDate must be <= toDate", nameof(fromDate));

        var habitNode = await nodes.GetAsync(habitNodeId, ct).ConfigureAwait(false);
        if (habitNode is null || habitNode.NodeType != NodeType.Habit)
            return Array.Empty<StreakRun>();

        var cfg = await configs.GetAsync(habitNodeId, ct).ConfigureAwait(false);
        if (cfg is null) return Array.Empty<StreakRun>();

        var history = await configs.ListHistoryAsync(habitNodeId, ct).ConfigureAwait(false);
        var timeline = new HabitConfigTimeline(cfg, history);
        var habitCreatedDate = DateOnly.FromDateTime(habitNode.CreatedAt);

        var heatmap = await completions.GetHeatmapAsync(habitNodeId, fromDate, toDate, ct).ConfigureAwait(false);
        var dailyLive = heatmap.ToDictionary(c => c.Date, c => c.Count - c.RetroactiveCount);

        var pauseList = await pauses.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(false);

        var runs = new List<StreakRun>();
        DateOnly? runStart = null;
        var runEnd = default(DateOnly);
        var length = 0;
        var recoveriesUsed = 0;

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            var cfgForDay = timeline.ResolveFor(day);
            var rules = new ScheduledRules(
                cfgForDay.ScheduleType,
                cfgForDay.ScheduleDaysJson,
                cfgForDay.ScheduleWeeklyTarget,
                cfgForDay.ScheduleEveryXDays,
                cfgForDay.ScheduleMonthlyDaysJson,
                habitCreatedDate);

            var isScheduled = scheduler.IsScheduled(day, rules);
            var completedCount = dailyLive.GetValueOrDefault(day, 0);
            var verdict = DailyVerdict(isScheduled, completedCount);

            if (verdict == DayVerdict.Neutral) continue;

            if (verdict == DayVerdict.Counts)
            {
                runStart ??= day;
                runEnd = day;
                length++;
                continue;
            }

            // DayVerdict.Misses — None policy: always close the open run.
            if (runStart is { } rs)
            {
                runs.Add(new StreakRun(
                    rs, runEnd, length,
                    IsCurrent: false,
                    RecoveriesUsed: recoveriesUsed,
                    BreakReason: StreakBreakReason.Missed));
                runStart = null;
                length = 0;
                recoveriesUsed = 0;
            }
        }

        if (runStart is { } rsFinal)
        {
            var isCurrent = runEnd >= today.AddDays(-1);
            runs.Add(new StreakRun(rsFinal, runEnd, length, isCurrent, recoveriesUsed, null));
        }

        _ = pauseList; // wired in later tasks
        return runs;
    }

    private static DayVerdict DailyVerdict(bool isScheduled, int completedCount)
    {
        if (!isScheduled) return DayVerdict.Neutral;
        return completedCount > 0 ? DayVerdict.Counts : DayVerdict.Misses;
    }
}
