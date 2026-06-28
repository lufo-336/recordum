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
    IAbstinenceService abstinence,
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

        if (habitNode.HabitType == HabitType.Resistance)
            return await ComputeAbstinenceAsync(habitNode, fromDate, toDate, ct).ConfigureAwait(false);

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

            var isPaused = pauseList.Any(p => day >= p.PauseFrom && day <= p.PauseTo);
            if (isPaused)
            {
                if (runStart is not null) runEnd = day;
                continue;
            }

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

        return runs;
    }

    // Abstinence (Resistance): clean runs come from abstinence_period rows. Each period [start, end]
    // (or [start, today] if open) is a clean run; the gap between a period's end and the next start is
    // "relapsed, not resumed" and is not counted. Pauses freeze a run (neither clean nor break). The
    // open period is the current run. Schedule/mode/recovery do not apply. See spec 2026-06-28.
    private async ValueTask<IReadOnlyList<StreakRun>> ComputeAbstinenceAsync(
        HabitNode habitNode, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var createdDate = DateOnly.FromDateTime(habitNode.CreatedAt);
        var lifeStart = createdDate < fromDate ? createdDate : fromDate; // full life for true lengths (CR11)
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var periods = await abstinence.ListPeriodsAsync(habitNode.Id, lifeStart, toDate, ct).ConfigureAwait(false);
        if (periods.Count == 0) return Array.Empty<StreakRun>();

        var pauseList = await pauses.ListByHabitAsync(habitNode.Id, ct).ConfigureAwait(false);
        bool IsPaused(DateOnly d) => pauseList.Any(p => d >= p.PauseFrom && d <= p.PauseTo);

        int CleanDays(DateOnly s, DateOnly e)
        {
            var n = 0;
            for (var d = s; d <= e; d = d.AddDays(1))
                if (!IsPaused(d)) n++;
            return n;
        }

        var runs = new List<StreakRun>();
        foreach (var p in periods)
        {
            var startDay = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.StartedAtMs).UtcDateTime);
            var isOpen = p.EndedAtMs is null;
            var endDay = isOpen
                ? today
                : DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(p.EndedAtMs!.Value).UtcDateTime);
            if (startDay > toDate) continue;
            if (endDay < startDay) endDay = startDay;

            var length = CleanDays(startDay, endDay);
            runs.Add(new StreakRun(startDay, endDay, length, isOpen, 0, isOpen ? null : StreakBreakReason.Lapse));
        }

        return runs.Where(r => r.End >= fromDate).ToList();
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
