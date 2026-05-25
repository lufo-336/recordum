using Microsoft.Extensions.DependencyInjection;
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
    [FromKeyedServices(StreakMode.Daily)] IStreakModeEvaluator dailyEval,
    [FromKeyedServices(StreakMode.Weekly)] IStreakModeEvaluator weeklyEval,
    [FromKeyedServices(StreakMode.Occurrences)] IStreakModeEvaluator occurrencesEval,
    [FromKeyedServices(StreakMode.Both)] IStreakModeEvaluator bothEval,
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

        var modeEvaluators = new Dictionary<StreakMode, IStreakModeEvaluator>
        {
            [StreakMode.Daily] = dailyEval,
            [StreakMode.Weekly] = weeklyEval,
            [StreakMode.Occurrences] = occurrencesEval,
            [StreakMode.Both] = bothEval,
        };

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
            var weekState = BuildWeekState(day, cfgForDay, dailyLive, habitCreatedDate);
            var ctx = new DayContext(day, isScheduled, IsPaused: false, CompletedValue: 0m,
                                     CompletedCount: completedCount, WeekState: weekState);
            var verdict = modeEvaluators[cfgForDay.StreakMode].Evaluate(ctx);

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

    private static WeeklyPeriodState? BuildWeekState(
        DateOnly day, HabitConfig cfg, IReadOnlyDictionary<DateOnly, int> dailyLive, DateOnly createdDate)
    {
        if (cfg.StreakMode != StreakMode.Weekly && cfg.StreakMode != StreakMode.Both) return null;
        var target = cfg.ScheduleWeeklyTarget ?? 1;
        // ISO week boundaries: Monday..Sunday.
        var dow = ((int)day.DayOfWeek + 6) % 7; // Mon=0
        var weekStart = day.AddDays(-dow);
        var weekEnd = weekStart.AddDays(6);
        var completionsSoFar = 0;
        for (var d = weekStart; d <= day; d = d.AddDays(1))
            if (d >= createdDate) completionsSoFar += dailyLive.GetValueOrDefault(d, 0);
        return new WeeklyPeriodState(target, completionsSoFar, day == weekEnd);
    }
}
