using FluentAssertions;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class AbstinencePeriodListViewModelTests
{
    private static AbstinencePeriod P(Guid habit, DateTime start, DateTime? end) => new()
    {
        Id = Guid.NewGuid(), HabitNodeId = habit,
        StartedAtMs = new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        EndedAtMs = end is { } e ? new DateTimeOffset(e, TimeSpan.Zero).ToUnixTimeMilliseconds() : null,
    };

    [Fact]
    public async Task LoadRange_orders_open_first_then_newest()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
        var habit = Guid.NewGuid();
        var abstinence = new FakeAbstinenceService();
        abstinence.Periods[habit] = new List<AbstinencePeriod>
        {
            P(habit, new DateTime(2026, 5, 1, 0,0,0, DateTimeKind.Utc), new DateTime(2026, 5, 6, 0,0,0, DateTimeKind.Utc)),   // older closed
            P(habit, new DateTime(2026, 5, 10, 0,0,0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 0,0,0, DateTimeKind.Utc)), // newer closed
            P(habit, new DateTime(2026, 6, 10, 0,0,0, DateTimeKind.Utc), null),                                              // open
        };
        var sut = new AbstinencePeriodListViewModel(abstinence, new FakeConfirmationDialog(), clock);

        await sut.LoadRangeAsync(habit, new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 30));

        sut.Rows.Should().HaveCount(3);
        sut.Rows[0].IsCurrent.Should().BeTrue();               // open pinned first
        sut.Rows[1].RelapsedOnDisplay.Should().NotBeEmpty();   // then newest closed
        sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_when_no_periods()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
        var sut = new AbstinencePeriodListViewModel(new FakeAbstinenceService(), new FakeConfirmationDialog(), clock);

        await sut.LoadRangeAsync(Guid.NewGuid(), null, null);

        sut.Rows.Should().BeEmpty();
        sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_row_removes_it_from_the_list()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero));
        var habit = Guid.NewGuid();
        var abstinence = new FakeAbstinenceService();
        abstinence.Periods[habit] = new List<AbstinencePeriod>
        {
            P(habit, new DateTime(2026, 5, 10, 0,0,0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 0,0,0, DateTimeKind.Utc)),
        };
        var sut = new AbstinencePeriodListViewModel(abstinence, new FakeConfirmationDialog { Result = true }, clock);
        await sut.LoadRangeAsync(habit, null, null);

        await sut.Rows[0].DeleteCommand.ExecuteAsync(null);

        sut.Rows.Should().BeEmpty();
    }
}
