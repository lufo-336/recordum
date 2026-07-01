using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class AbstinencePeriodRowViewModelTests
{
    private static AbstinencePeriod Closed(DateTime start, DateTime end) => new()
    {
        Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(),
        StartedAtMs = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        EndedAtMs = new DateTimeOffset(end, TimeSpan.Zero).ToUnixTimeMilliseconds(),
    };

    [Fact]
    public void Closed_period_shows_elapsed_interval_and_relapse()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
        var p = Closed(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
        var sut = new AbstinencePeriodRowViewModel(p, new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

        sut.IsCurrent.Should().BeFalse();
        sut.DurationDisplay.Should().Be("2g 0:00:00"); // 05-10 00:00 → 05-12 00:00 = exactly 2 days elapsed (interval, not a day count)
        sut.RelapsedOnDisplay.Should().NotBeEmpty();
        sut.SinceDisplay.Should().NotBeEmpty();
    }

    [Fact]
    public void Closed_same_day_period_shows_hms_interval_not_a_day_count()
    {
        // A period started and relapsed the same day must read the actual elapsed time,
        // not "1 clean day" (the old calendar-day count bug).
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 10, 20, 0, 0, TimeSpan.Zero));
        var p = Closed(new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 10, 12, 20, 0, DateTimeKind.Utc));
        var sut = new AbstinencePeriodRowViewModel(p, new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

        sut.DurationDisplay.Should().Be("3:20:00"); // 3h 20m
    }

    [Fact]
    public void Open_period_is_current_and_clock_runs()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var p = new AbstinencePeriod
        {
            Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(),
            StartedAtMs = new DateTimeOffset(new DateTime(2026, 5, 20, 6, 0, 0, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
            EndedAtMs = null,
        };
        var sut = new AbstinencePeriodRowViewModel(p, new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

        sut.IsCurrent.Should().BeTrue();
        sut.Clock.Display.Should().Be("1g 6:00:00");
    }

    [Fact]
    public async Task Delete_confirmed_calls_service_and_onDeleted_and_raises_changed()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
        var p = Closed(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
        var abstinence = new FakeAbstinenceService();
        abstinence.Periods[p.HabitNodeId] = new List<AbstinencePeriod> { p };
        var confirm = new FakeConfirmationDialog { Result = true };
        var removed = false; var changed = false;
        var sut = new AbstinencePeriodRowViewModel(p, abstinence, confirm, clock, _ => removed = true);
        sut.Changed += (_, _) => changed = true;

        await sut.DeleteCommand.ExecuteAsync(null);

        confirm.Calls.Should().Be(1);
        abstinence.Periods[p.HabitNodeId].Should().BeEmpty();
        removed.Should().BeTrue();
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_cancelled_does_nothing()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
        var p = Closed(new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc));
        var abstinence = new FakeAbstinenceService();
        abstinence.Periods[p.HabitNodeId] = new List<AbstinencePeriod> { p };
        var sut = new AbstinencePeriodRowViewModel(p, abstinence, new FakeConfirmationDialog { Result = false }, clock, _ => { });

        await sut.DeleteCommand.ExecuteAsync(null);

        abstinence.Periods[p.HabitNodeId].Should().ContainSingle();
    }
}
