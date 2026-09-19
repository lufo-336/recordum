using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Today;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Today;

public class ResistanceTodayContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (ResistanceTodayContent Sut, FakeAbstinenceService Abstinence, HabitNode Node) NewSut()
    {
        var abstinence = new FakeAbstinenceService();
        var clock = new FakeClock(Now);
        var node = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "r" };
        return (new ResistanceTodayContent(abstinence, clock), abstinence, node);
    }

    [Fact]
    public async Task An_open_period_reads_as_Abstaining_with_a_running_clock()
    {
        var (sut, abstinence, node) = NewSut();
        var startedMs = Now.AddDays(-3).ToUnixTimeMilliseconds();
        abstinence.Periods[node.Id] = new()
        {
            new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = node.Id, StartedAtMs = startedMs, EndedAtMs = null },
        };
        abstinence.States[node.Id] = new AbstinenceState(AbstinenceStatus.Abstaining, startedMs, null);

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.Status.Should().Be(AbstinenceStatus.Abstaining);
        sut.CanLapse.Should().BeTrue();
        sut.CanStart.Should().BeFalse();
        sut.Clock.Display.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Start_when_NotStarted_opens_a_period_and_raises_Changed()
    {
        var (sut, abstinence, node) = NewSut();
        await sut.LoadAsync(node, DayBoundary.Utc);
        sut.Status.Should().Be(AbstinenceStatus.NotStarted);
        var raised = 0;
        sut.Changed += (_, _) => raised++;

        await sut.StartCommand.ExecuteAsync(null);

        sut.Status.Should().Be(AbstinenceStatus.Abstaining);
        abstinence.Periods[node.Id].Should().ContainSingle(p => p.EndedAtMs == null);
        raised.Should().Be(1);
    }

    [Fact]
    public async Task Lapse_when_Abstaining_closes_the_period_and_raises_Changed()
    {
        var (sut, abstinence, node) = NewSut();
        await abstinence.StartAsync(node.Id);
        await sut.LoadAsync(node, DayBoundary.Utc);
        sut.CanLapse.Should().BeTrue();
        var raised = 0;
        sut.Changed += (_, _) => raised++;

        await sut.LapseCommand.ExecuteAsync(null);

        sut.Status.Should().Be(AbstinenceStatus.Relapsed);
        abstinence.Periods[node.Id].Should().OnlyContain(p => p.EndedAtMs != null);
        raised.Should().Be(1);
    }

    [Fact]
    public async Task AttemptsLast90Days_counts_periods_started_within_90_days()
    {
        var (sut, abstinence, node) = NewSut();
        abstinence.Periods[node.Id] = new()
        {
            new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = node.Id, StartedAtMs = Now.AddDays(-10).ToUnixTimeMilliseconds(), EndedAtMs = Now.AddDays(-9).ToUnixTimeMilliseconds() },
            new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = node.Id, StartedAtMs = Now.AddDays(-40).ToUnixTimeMilliseconds(), EndedAtMs = Now.AddDays(-39).ToUnixTimeMilliseconds() },
            // started 200 days ago → outside the 90-day window, not counted
            new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = node.Id, StartedAtMs = Now.AddDays(-200).ToUnixTimeMilliseconds(), EndedAtMs = Now.AddDays(-199).ToUnixTimeMilliseconds() },
        };

        await sut.LoadAsync(node, DayBoundary.Utc);

        sut.AttemptsLast90Days.Should().Be(2);
    }
}
