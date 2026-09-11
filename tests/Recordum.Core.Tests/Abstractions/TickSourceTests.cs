using Recordum.Core.Abstractions;
using Xunit;

namespace Recordum.Core.Tests.Abstractions;

public class TickSourceTests
{
    [Fact]
    public void Null_factory_produces_a_source_that_never_ticks()
    {
        var ticks = 0;
        using var source = NullTickSourceFactory.Instance.Create(TimeSpan.FromMilliseconds(1));
        source.Tick += (_, _) => ticks++;

        source.Start();

        // Non è un test sul tempo che passa: NullTickSource non ha un timer, quindi non c'è nulla
        // da attendere. Start() su di lui è un no-op per costruzione, ed è ciò che serve a un
        // ViewModel in un test headless o a un'anteprima inerte del builder.
        Assert.Equal(0, ticks);
    }

    [Fact]
    public void Fake_factory_ticks_only_when_told_to()
    {
        var factory = new FakeTickSourceFactory();
        var ticks = 0;
        using var source = factory.Create(TimeSpan.FromSeconds(1));
        source.Tick += (_, _) => ticks++;

        factory.TickAll();          // non avviato: nessun tick
        source.Start();
        factory.TickAll();
        factory.TickAll();
        source.Stop();
        factory.TickAll();          // fermato: nessun tick

        Assert.Equal(2, ticks);
    }

    [Fact]
    public void Fake_factory_records_the_requested_interval()
    {
        var factory = new FakeTickSourceFactory();
        using var a = factory.Create(TimeSpan.FromSeconds(1));
        using var b = factory.Create(TimeSpan.FromMilliseconds(300));

        // Il debounce dell'anteprima grafici chiede 300ms, gli orologi 1s: chi cuce l'astrazione
        // deve poter provare che l'intervallo giusto è arrivato fin qui.
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(300)], factory.RequestedIntervals);
    }

    [Fact]
    public void Disposing_a_source_stops_it()
    {
        var factory = new FakeTickSourceFactory();
        var ticks = 0;
        var source = factory.Create(TimeSpan.FromSeconds(1));
        source.Tick += (_, _) => ticks++;
        source.Start();

        source.Dispose();
        factory.TickAll();

        Assert.Equal(0, ticks);
    }

    [Fact]
    public void A_disposed_source_does_not_restart()
    {
        var factory = new FakeTickSourceFactory();
        var ticks = 0;
        var source = factory.Create(TimeSpan.FromSeconds(1));
        source.Tick += (_, _) => ticks++;

        source.Dispose();
        source.Start();      // misuso: dopo Dispose non deve riaccendere niente
        factory.TickAll();

        Assert.Equal(0, ticks);
    }
}
