using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Recordum.WPF.ViewModels.Home;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Home;

public class HomeClockTickerTests
{
    [Fact]
    public void TickNow_invokes_all_registered_callbacks()
    {
        using var ticker = new HomeClockTicker(null);
        int a = 0, b = 0;
        ticker.Register(() => a++);
        ticker.Register(() => b++);

        ticker.TickNow();
        ticker.TickNow();

        a.Should().Be(2);
        b.Should().Be(2);
    }

    [Fact]
    public void Clear_stops_fan_out()
    {
        using var ticker = new HomeClockTicker(null);
        int a = 0;
        ticker.Register(() => a++);

        ticker.Clear();
        ticker.TickNow();

        a.Should().Be(0);
    }

    [Fact]
    public void Dispose_stops_fan_out()
    {
        var ticker = new HomeClockTicker(null);
        int a = 0;
        ticker.Register(() => a++);

        ticker.Dispose();
        ticker.TickNow();

        a.Should().Be(0);
    }

    [Fact]
    public void Registered_resistance_row_updates_clock_on_tick_with_seconds()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var sinceMs = clock.UtcNowMs;
        var row = new HomeRowViewModel(ResistanceRow(Guid.NewGuid(), sinceMs), 0,
            new FakeHabitNodeService(), new FakePurgeService(), uiPrefs: null, streaks: null, clock: clock);
        row.IsResistanceClock.Should().BeTrue();
        row.KeyFigureText.Should().Be("0:00:00");

        using var ticker = new HomeClockTicker(null);
        ticker.Register(row.RefreshClock);

        // one second later the display must visibly change (seconds shown so the row ticks live)
        clock.Advance(TimeSpan.FromSeconds(9));
        ticker.TickNow();
        row.KeyFigureText.Should().Be("0:00:09");

        clock.Advance(new TimeSpan(20, 4, 12, 0));
        ticker.TickNow();
        row.KeyFigureText.Should().Be("20g 4:12:09");
    }

    private static HomeTreeRow ResistanceRow(Guid id, long sinceMs) =>
        new(id, null, "r", "#000000", HabitType.Resistance, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            AbstinenceStatus: AbstinenceStatus.Abstaining, AbstinenceOpenSinceMs: sinceMs);
}
