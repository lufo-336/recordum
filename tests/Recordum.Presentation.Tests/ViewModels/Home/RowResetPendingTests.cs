using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Tests.Abstractions;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Home;

/// <summary>"reset 5:00" nella riga il cui giorno è indietro rispetto a quello dell'app (decisione dell'autore, 2026-10-30).
/// App a mezzanotte; abitudine con reset alle 05:00.</summary>
public sealed class RowResetPendingTests : IDisposable
{
    private static readonly DayBoundary AppDay = new(TimeZoneInfo.Utc, TimeOnly.MinValue);
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;

    public RowResetPendingTests() => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static HomeTreeRow Row(TimeOnly habitReset) =>
        new(Guid.NewGuid(), null, "x", "#000000", HabitType.Action, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            HabitDay: new DayBoundary(TimeZoneInfo.Utc, habitReset), AppDay: AppDay);

    private static async Task<(HomeViewModel Home, FakeClock Clock, FakeTickSourceFactory Ticks)> LoadAsync(TimeOnly habitReset, int hour)
    {
        var query = new FakeHabitTreeQueryService();
        query.HomeRows.Add(Row(habitReset));
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 28, hour, 0, 0, TimeSpan.Zero));
        var ticks = new FakeTickSourceFactory();
        var home = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(), clock: clock, ticks: ticks);
        await home.LoadCommand.ExecuteAsync(null);
        return (home, clock, ticks);
    }

    [Fact]
    public async Task Row_shows_reset_while_its_day_lags()
    {
        var (home, clock, ticks) = await LoadAsync(new TimeOnly(5, 0), hour: 2);
        var row = home.Rows.Single();

        row.IsDayBehindApp.Should().BeTrue();
        row.ResetPendingText.Should().Be("reset 05:00");
        row.ResetPendingTip.Should().Be("Per questa abitudine la giornata finisce alle 05:00: l'ultima casella è ancora ieri.");

        clock.Set(new DateTimeOffset(2026, 10, 28, 6, 0, 0, TimeSpan.Zero));
        ticks.TickAll();

        row.IsDayBehindApp.Should().BeFalse();
        row.ResetPendingText.Should().BeNull();
    }

    [Fact]
    public async Task Same_reset_never_shows_it()
    {
        var (home, _, ticks) = await LoadAsync(TimeOnly.MinValue, hour: 2);

        home.Rows.Single().IsDayBehindApp.Should().BeFalse();
        home.Rows.Single().TracksDayLag.Should().BeFalse();
        ticks.RunningCount.Should().Be(0, "senza ritardo possibile la riga non chiede il battito");
    }
}
