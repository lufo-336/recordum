using FluentAssertions;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.Services;

public class FavoriteColorStoreTests
{
    private static FavoriteColorStore NewSut() => new(new FakeAppSettingService());

    [Fact]
    public async Task Add_appends_and_dedups()
    {
        var sut = NewSut();
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00FF00");
        await sut.AddAsync("#ff0000");           // dup → no-op, order kept
        (await sut.GetAsync()).Should().Equal("#ff0000", "#00ff00");
    }

    [Fact]
    public async Task Remove_drops_the_color()
    {
        var sut = NewSut();
        await sut.AddAsync("#ff0000");
        await sut.AddAsync("#00ff00");
        await sut.RemoveAsync("#FF0000");
        (await sut.GetAsync()).Should().Equal("#00ff00");
    }

    [Fact]
    public async Task Caps_at_twelve()
    {
        var sut = NewSut();
        for (int i = 0; i < 15; i++) await sut.AddAsync($"#0000{i:x2}");
        (await sut.GetAsync()).Should().HaveCount(12);
    }

    [Fact]
    public async Task Invalid_hex_ignored()
    {
        var sut = NewSut();
        await sut.AddAsync("nope");
        (await sut.GetAsync()).Should().BeEmpty();
    }
}
