using System.Globalization;
using Recordum.Core.Tests.Abstractions;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.Home;

namespace Recordum.Presentation.Tests;

/// <summary>Prima di ITickSource questi comportamenti NON erano provabili: il timer nasceva solo
/// sotto un'applicazione WPF viva, quindi in headless non batteva e i test potevano al massimo
/// pilotare il fan-out a mano (<c>TickNow</c>). Ora il battito è iniettato, quindi si prova davvero
/// che la sorgente venga avviata e che il suo tick raggiunga i consumatori.</summary>
public class TickingClockTests
{
    [Fact]
    public void Home_ticker_fans_a_real_tick_out_to_every_registered_row()
    {
        var ticks = new FakeTickSourceFactory();
        using var sut = new HomeClockTicker(ticks);
        var a = 0;
        var b = 0;
        sut.Register(() => a++);
        sut.Register(() => b++);

        ticks.TickAll();

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void Home_ticker_stops_beating_after_clear()
    {
        var ticks = new FakeTickSourceFactory();
        using var sut = new HomeClockTicker(ticks);
        var a = 0;
        sut.Register(() => a++);
        Assert.Equal(1, ticks.RunningCount);   // Register avvia la sorgente

        sut.Clear();

        // Si osserva la SORGENTE: svuotare le callback da solo basterebbe a tenere a == 0, e un
        // Stop() cancellato lascerebbe una sorgente che batte a vuoto per sempre.
        Assert.Equal(0, ticks.RunningCount);
        ticks.TickAll();
        Assert.Equal(0, a);
    }

    [Fact]
    public void Home_ticker_disposes_its_source()
    {
        var ticks = new FakeTickSourceFactory();
        var sut = new HomeClockTicker(ticks);
        sut.Register(() => { });
        Assert.Equal(1, ticks.RunningCount);

        sut.Dispose();

        Assert.Equal(0, ticks.RunningCount);
    }

    [Fact]
    public void Live_clock_refreshes_its_display_on_a_real_tick_and_only_then()
    {
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-10T12:00:00Z", CultureInfo.InvariantCulture));
        using var sut = new LiveElapsedClock(clock, ticks);

        sut.Start(clock.UtcNowMs - 5_000);
        var atStart = sut.Display;
        Assert.Equal(DurationFormat.FormatStopwatch(TimeSpan.FromSeconds(5)), atStart);

        clock.Advance(TimeSpan.FromSeconds(60));
        // Il tempo è avanzato ma nessun battito è arrivato: il display non si muove da solo.
        Assert.Equal(atStart, sut.Display);

        ticks.TickAll();

        // Discrimina in due modi: senza sorgente avviata/collegata il display resterebbe atStart;
        // e il valore atteso è quello vero (65 s), non un semplice "diverso".
        Assert.NotEqual(atStart, sut.Display);
        Assert.Equal(DurationFormat.FormatStopwatch(TimeSpan.FromSeconds(65)), sut.Display);
    }

    [Fact]
    public void Live_clock_stops_refreshing_after_stop()
    {
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-10T12:00:00Z", CultureInfo.InvariantCulture));
        using var sut = new LiveElapsedClock(clock, ticks);
        sut.Start(clock.UtcNowMs - 5_000);
        Assert.Equal(1, ticks.RunningCount);

        sut.Stop();

        // Il Display vuoto verrebbe comunque da Stop() (azzera _startedMs): la prova che la sorgente
        // smette davvero di battere e' lo stato della sorgente.
        Assert.Equal(0, ticks.RunningCount);
        clock.Advance(TimeSpan.FromSeconds(60));
        ticks.TickAll();
        Assert.Equal(string.Empty, sut.Display);
    }

    [Fact]
    public void Live_clock_disposes_its_source()
    {
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-10T12:00:00Z", CultureInfo.InvariantCulture));
        var sut = new LiveElapsedClock(clock, ticks);
        sut.Start(clock.UtcNowMs - 5_000);
        Assert.Equal(1, ticks.RunningCount);

        sut.Dispose();

        Assert.Equal(0, ticks.RunningCount);
    }
}
