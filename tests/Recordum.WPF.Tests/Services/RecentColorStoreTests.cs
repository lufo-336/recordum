using FluentAssertions;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Xunit;

namespace Recordum.WPF.Tests.Services;

public class RecentColorStoreTests
{
    private static RecentColorStore NewSut(out FakeAppSettingService settings)
    {
        settings = new FakeAppSettingService();
        return new RecentColorStore(settings);
    }

    [Fact]
    public async Task AddAsync_puts_color_at_front()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00ff00");
        (await sut.GetAsync()).Should().ContainInOrder("#00ff00", "#ff0000");
    }

    [Fact]
    public async Task AddAsync_dedups_and_promotes_existing()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00ff00");
        await sut.AddAsync("#ff0000"); // re-add → moves to front, no dup
        (await sut.GetAsync()).Should().Equal("#ff0000", "#00ff00");
    }

    [Fact]
    public async Task AddAsync_caps_at_eight()
    {
        var sut = NewSut(out _);
        for (int i = 0; i < 10; i++) await sut.AddAsync($"#0000{i:x2}");
        var recents = await sut.GetAsync();
        recents.Should().HaveCount(8);
        recents[0].Should().Be("#000009"); // newest
    }

    [Fact]
    public async Task AddAsync_ignores_invalid_hex()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("not-a-color");
        await sut.AddAsync("#fff"); // 3-digit not supported → invalid
        (await sut.GetAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_normalizes_to_lowercase()
    {
        var sut = NewSut(out _);
        await sut.AddAsync("#AABBCC");
        (await sut.GetAsync()).Should().Equal("#aabbcc");
    }

    [Fact]
    public async Task GetAsync_roundtrips_persisted_csv()
    {
        var sut = NewSut(out var settings);
        settings.Store["ui.recent_colors"] = "#111111,#222222";
        (await sut.GetAsync()).Should().Equal("#111111", "#222222");
    }
}
