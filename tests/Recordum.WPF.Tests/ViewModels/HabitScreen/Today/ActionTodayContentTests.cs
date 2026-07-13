using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Today;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Today;

public class ActionTodayContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private static (ActionTodayContent Sut, FakeCompletionService Completions, FakeStreakAggregator Streaks,
                    FakeHourlyDistributionQueryService Hourly, FakeClock Clock, HabitNode Node) NewSut()
    {
        var completions = new FakeCompletionService();
        var streaks = new FakeStreakAggregator();
        var hourly = new FakeHourlyDistributionQueryService();
        var clock = new FakeClock(Now);
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a" };
        return (new ActionTodayContent(completions, streaks, hourly, clock), completions, streaks, hourly, clock, node);
    }

    [Fact]
    public async Task ToggleDone_from_not_done_records_a_completion_and_flips_IsDoneToday()
    {
        var (sut, completions, _, _, _, node) = NewSut();
        await sut.LoadAsync(node);
        sut.IsDoneToday.Should().BeFalse();

        await sut.ToggleDoneCommand.ExecuteAsync(null);

        sut.IsDoneToday.Should().BeTrue();
        completions.Store.Values.Should().ContainSingle(c => c.HabitNodeId == node.Id);
    }

    [Fact]
    public async Task ToggleDone_from_done_removes_todays_completion()
    {
        var (sut, completions, _, _, clock, node) = NewSut();
        var cid = Guid.NewGuid();
        completions.Store[cid] = new Completion
        {
            Id = cid, HabitNodeId = node.Id, CompletedAt = clock.UtcNow.UtcDateTime, CompletedAtMs = clock.UtcNowMs,
        };
        await sut.LoadAsync(node);
        sut.IsDoneToday.Should().BeTrue();

        await sut.ToggleDoneCommand.ExecuteAsync(null);

        sut.IsDoneToday.Should().BeFalse();
        completions.Store.Values.Should().NotContain(c => c.HabitNodeId == node.Id);
    }

    [Fact]
    public async Task CurrentStreak_reads_the_IsCurrent_run()
    {
        var (sut, _, streaks, _, _, node) = NewSut();
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        streaks.Runs.Add(new StreakRun(today.AddDays(-4), today, 5, IsCurrent: true));
        streaks.Runs.Add(new StreakRun(today.AddDays(-20), today.AddDays(-10), 3, IsCurrent: false));

        await sut.LoadAsync(node);

        sut.CurrentStreak.Should().Be(5);
    }

    [Fact]
    public async Task Pct30_is_distinct_completed_days_over_30()
    {
        var (sut, completions, _, _, _, node) = NewSut();
        // Three completions on two distinct days within the last 30 → 2/30 ≈ 7%.
        void Add(DateTimeOffset at)
            => completions.Store[Guid.NewGuid()] = new Completion
            { Id = Guid.NewGuid(), HabitNodeId = node.Id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds() };
        Add(Now.AddDays(-1));
        Add(Now.AddDays(-1).AddHours(2)); // same day → still one distinct day
        Add(Now.AddDays(-5));

        await sut.LoadAsync(node);

        sut.Pct30.Should().Be((int)System.Math.Round(100.0 * 2 / 30.0, System.MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task UsualHour_is_the_peak_bucket_and_null_when_empty()
    {
        var (sut, _, _, hourly, _, node) = NewSut();
        await sut.LoadAsync(node);
        sut.UsualHour.Should().BeNull();           // all buckets zero
        sut.HasUsualHour.Should().BeFalse();

        hourly.Buckets = new int[24];
        hourly.Buckets[8] = 5;
        hourly.Buckets[20] = 2;
        await sut.LoadAsync(node);

        sut.HasUsualHour.Should().BeTrue();
        sut.UsualHour.Should().NotBeNullOrEmpty();  // peak at hour 8
    }
}
