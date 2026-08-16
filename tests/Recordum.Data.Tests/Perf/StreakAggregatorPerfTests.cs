using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Aggregators;
using Recordum.Data.Services.Evaluators;
using Recordum.Data.Services.Policies;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Perf;

[Trait("category", "perf")]
public sealed class StreakAggregatorPerfTests
{
    [Fact]
    public async Task OneYearHistory_completes_within_budget()
    {
        var start = new DateOnly(2025, 5, 1);
        // FakeClock anchored a day past the last completion so the aggregator's "today" window covers the full range.
        var clock = new FakeClock(new DateTimeOffset(start.AddDays(365).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        await using var fx = new DbFixture(clock);

        var habitId = Guid.NewGuid();
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(new HabitNode
            {
                Id = habitId,
                NodeType = NodeType.Habit,
                HabitType = HabitType.Action,
                Name = "perf",
            });
            await db.SaveChangesAsync();
        }

        var configs = new HabitConfigService(fx.Factory, fx.Clock, NullLogger<HabitConfigService>.Instance);
        await configs.UpsertAsync(habitId, c =>
        {
            c.ScheduleType = ScheduleType.Daily;
            c.StreakMode = StreakMode.Daily;
            c.StreakRecovery = StreakRecovery.None;
        });

        var completions = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        for (var i = 0; i < 365; i++)
        {
            var when = start.AddDays(i).ToDateTime(new TimeOnly(10, 0));
            await completions.RecordAsync(new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                CompletedAt = when,
                CompletedAtMs = new DateTimeOffset(when, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                IsRetroactive = false,
            });
        }

        var aggr = BuildAggregator(fx, configs);

        // Warm-up.
        _ = await aggr.GetHistoryAsync(habitId, start, start.AddDays(364));

        var samples = new long[50];
        for (var i = 0; i < samples.Length; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = await aggr.GetHistoryAsync(habitId, start, start.AddDays(364));
            sw.Stop();
            samples[i] = sw.ElapsedMilliseconds;
        }
        Array.Sort(samples);
        var p95 = samples[(int)(samples.Length * 0.95) - 1];
        Assert.True(p95 < 200, $"p95={p95}ms exceeds 200ms cap for 1-year history (budget 100ms + slack)");
    }

    private static SchedulingAwareStreakAggregator BuildAggregator(DbFixture fx, IHabitConfigService configs)
    {
        var nodes = new HabitNodeService(
            fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance);
        var completions = new CompletionService(
            fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus());
        var pauses = new StreakPauseService(
            fx.Factory, fx.Clock, NullLogger<StreakPauseService>.Instance);
        var scheduler = new ScheduleEvaluator();
        var dailyEval = new DailyStreakModeEvaluator();
        var weeklyEval = new WeeklyStreakModeEvaluator();
        var occEval = new OccurrencesStreakModeEvaluator();
        var bothEval = new BothStreakModeEvaluator(dailyEval, weeklyEval);
        var noneRec = new NoneStreakRecoveryPolicy();
        var graceRec = new GraceDayStreakRecoveryPolicy();
        var manualRec = new ManualStreakRecoveryPolicy();
        var retroRec = new RetroactiveStreakRecoveryPolicy();
        var abstinence = new AbstinenceService(fx.Factory, fx.Clock, new RecordingEventBus());
        return new SchedulingAwareStreakAggregator(
            nodes, configs, completions, pauses, scheduler,
            dailyEval, weeklyEval, occEval, bothEval,
            noneRec, graceRec, manualRec, retroRec, abstinence, fx.Clock);
    }
}
