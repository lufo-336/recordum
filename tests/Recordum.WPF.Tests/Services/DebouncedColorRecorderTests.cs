using FluentAssertions;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.Services;

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
        await Task.Delay(150);

        (await store.GetAsync()).Should().Equal("#3b82f6");
    }

    [Fact]
    public async Task A_settled_color_is_recorded_after_the_quiet_period()
    {
        var store = new RecentColorStore(new FakeAppSettingService());
        var sut = new DebouncedColorRecorder(store, delayMs: 20);

        sut.Record("#10b981");
        await Task.Delay(120);

        (await store.GetAsync()).Should().Contain("#10b981");
    }
}
