using FluentAssertions;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.Services;

public class DebouncedColorRecorderTests
{
    [Fact]
    public async Task Only_the_last_color_in_a_burst_reaches_the_store()
    {
        // CR23: typing a hex / dragging the HSV fires many intermediate colours; only the settled one
        // should land in the recents MRU.
        var store = new RecentColorStore(new FakeAppSettingService());
        var sut = new DebouncedColorRecorder(store, delayMs: 30);

        sut.Record("#111111");
        sut.Record("#222222");
        sut.Record("#3b82f6"); // the colour the user settles on
        await WaitForAsync(store, "#3b82f6");

        // The earlier colours were cancelled before the settled one landed, so they can never appear.
        (await store.GetAsync()).Should().Equal("#3b82f6");
    }

    [Fact]
    public async Task A_settled_color_is_recorded_after_the_quiet_period()
    {
        var store = new RecentColorStore(new FakeAppSettingService());
        var sut = new DebouncedColorRecorder(store, delayMs: 20);

        sut.Record("#10b981");
        await WaitForAsync(store, "#10b981");

        (await store.GetAsync()).Should().Contain("#10b981");
    }

    private static async Task WaitForAsync(IRecentColorStore store, string hex)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!(await store.GetAsync()).Contains(hex))
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"{hex} was never recorded within 5 s");
            await Task.Delay(5);
        }
    }
}
