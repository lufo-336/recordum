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
    [FromKeyedServices(StreakRecovery.None)] IStreakRecoveryPolicy noneRec,
    [FromKeyedServices(StreakRecovery.GraceDay)] IStreakRecoveryPolicy graceRec,
    [FromKeyedServices(StreakRecovery.Manual)] IStreakRecoveryPolicy manualRec,
    [FromKeyedServices(StreakRecovery.Retroactive)] IStreakRecoveryPolicy retroRec,
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
        var dailyAll = heatmap.ToDictionary(c => c.Date, c => c.Count);

        var pauseList = await pauses.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(false);

        var modeEvaluators = new Dictionary<StreakMode, IStreakModeEvaluator>
        {
            [StreakMode.Daily] = dailyEval,
            [StreakMode.Weekly] = weeklyEval,
            [StreakMode.Occurrences] = occurrencesEval,
            [StreakMode.Both] = bothEval,
        };

        var recoveryPolicies = new Dictionary<StreakRecovery, IStreakRecoveryPolicy>
        {
            [StreakRecovery.None] = noneRec,
            [StreakRecovery.GraceDay] = graceRec,
            [StreakRecovery.Manual] = manualRec,
            [StreakRecovery.Retroactive] = retroRec,
        };

        var runs = new List<StreakRun>();
        DateOnly? runStart = null;
        var runEnd = default(DateOnly);
        var length = 0;
        var recoveriesUsed = 0;
        var pendingAbsorbs = 0;
        var missesInWindow = 0;
        var windowAnchor = (DateOnly?)null;

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
            var completedCount = cfgForDay.StreakRecovery == StreakRecovery.Retroactive
                ? dailyAll.GetValueOrDefault(day, 0)
                : dailyLive.GetValueOrDefault(day, 0);
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
                // Commit any pending absorptions: a follow-up Counts day "confirms"
                // them as recoveries that bridged the gap.
                recoveriesUsed += pendingAbsorbs;
                pendingAbsorbs = 0;
                if (windowAnchor is null || day >= windowAnchor.Value.AddDays(7))
                {
                    windowAnchor = day;
                    missesInWindow = 0;
                }
                continue;
            }

            // Misses
            if (cfgForDay.StreakRecovery == StreakRecovery.Manual)
            {
                if (cfgForDay.StreakBrokenAt is { } brokenAt
                    && DateOnly.FromDateTime(brokenAt) <= day
                    && runStart is { } rsm)
                {
                    runs.Add(new StreakRun(rsm, runEnd, length, false, recoveriesUsed, StreakBreakReason.Manual));
                    runStart = null; length = 0; recoveriesUsed = 0;
                    missesInWindow = 0; windowAnchor = null;
                }
                continue;
            }
            var policy = recoveryPolicies[cfgForDay.StreakRecovery];
            var absorbed = policy.TryAbsorb(new RecoveryContext(day, missesInWindow,
                cfgForDay.StreakBrokenAt, runStart ?? day));
            if (absorbed)
            {
                missesInWindow++;
                pendingAbsorbs++;
                continue;
            }
            if (runStart is { } rs)
            {
                runs.Add(new StreakRun(rs, runEnd, length, false, recoveriesUsed, StreakBreakReason.Missed));
                runStart = null; length = 0; recoveriesUsed = 0;
                pendingAbsorbs = 0;
                missesInWindow = 0; windowAnchor = null;
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
