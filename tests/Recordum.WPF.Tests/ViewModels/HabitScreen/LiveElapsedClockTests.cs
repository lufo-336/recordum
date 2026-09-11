using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class LiveElapsedClockTests
{
    // Uses the project's FakeClock. Tests pass a null ITickSourceFactory explicitly, so no tick
    // source is created; Start() still computes Display once.
    [Fact]
    public void Start_computes_display_from_started_instant()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var startedMs = new DateTimeOffset(2026, 5, 20, 6, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var sut = new LiveElapsedClock(clock, null);

        sut.Start(startedMs); // elapsed = 1 day 6 hours

        sut.Display.Should().Be("1g 6:00:00");
    }

    [Fact]
    public void Stop_clears_the_display()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var sut = new LiveElapsedClock(clock, null);
        sut.Start(clock.UtcNowMs);

        sut.Stop();

        sut.Display.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_clears_the_display_and_is_idempotent()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        var sut = new LiveElapsedClock(clock, null);
        sut.Start(clock.UtcNowMs);

        sut.Dispose();
        var second = () => sut.Dispose();

        sut.Display.Should().BeEmpty();
        second.Should().NotThrow(); // stopping/detaching an already-disposed clock is safe
    }
}
