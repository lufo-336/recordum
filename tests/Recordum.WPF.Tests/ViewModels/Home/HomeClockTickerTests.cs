using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.Formatting;
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
        using var ticker = new HomeClockTicker();
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
        using var ticker = new HomeClockTicker();
        int a = 0;
        ticker.Register(() => a++);

        ticker.Clear();
        ticker.TickNow();

        a.Should().Be(0);
    }

    [Fact]
    public void Dispose_stops_fan_out()
    {
        var ticker = new HomeClockTicker();
        int a = 0;
        ticker.Register(() => a++);

        ticker.Dispose();
        ticker.TickNow();

        a.Should().Be(0);
    }

    [Fact]
    public void FormatCompactDays_matches_days_h_mm()
    {
        DurationFormat.FormatCompactDays(new TimeSpan(20, 4, 12, 0)).Should().Be("20g 4:12");
        DurationFormat.FormatCompactDays(new TimeSpan(0, 4, 12, 0)).Should().Be("4:12");
        DurationFormat.FormatCompactDays(new TimeSpan(0, 0, 7, 0)).Should().Be("0:07");
    }

    [Fact]
    public void Registered_resistance_row_updates_clock_on_tick()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var sinceMs = clock.UtcNowMs;
        var row = new HomeRowViewModel(ResistanceRow(Guid.NewGuid(), sinceMs), 0,
            new FakeHabitNodeService(), new FakePurgeService(), uiPrefs: null, streaks: null, clock: clock);
        row.IsResistanceClock.Should().BeTrue();
        row.KeyFigureText.Should().Be("0:00");

        using var ticker = new HomeClockTicker();
        ticker.Register(row.RefreshClock);

        clock.Advance(new TimeSpan(20, 4, 12, 0));
        ticker.TickNow();

        row.KeyFigureText.Should().Be("20g 4:12");
    }

    private static HomeTreeRow ResistanceRow(Guid id, long sinceMs) =>
        new(id, null, "r", "#000000", HabitType.Resistance, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            AbstinenceStatus: AbstinenceStatus.Abstaining, AbstinenceOpenSinceMs: sinceMs);
}
