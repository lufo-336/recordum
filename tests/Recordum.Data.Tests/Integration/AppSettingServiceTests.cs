using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class AppSettingServiceTests
{
    private static AppSettingService Svc(DbFixture fx) =>
        new(fx.Factory, fx.Clock, NullLogger<AppSettingService>.Instance);

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_key()
    {
        await using var fx = new DbFixture();
        (await Svc(fx).GetAsync("does.not.exist")).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_then_GetAsync_round_trips()
    {
        await using var fx = new DbFixture();
        var sut = Svc(fx);
        await sut.SetAsync("ui.test", "hello");
        (await sut.GetAsync("ui.test")).Should().Be("hello");
    }

    [Fact]
    public async Task SetAsync_upserts_existing_key()
    {
        await using var fx = new DbFixture();
        var sut = Svc(fx);
        await sut.SetAsync("ui.test2", "v1");
        await sut.SetAsync("ui.test2", "v2");
        (await sut.GetAsync("ui.test2")).Should().Be("v2");
    }

    [Fact]
    public async Task SetEnumAsync_GetEnumAsync_round_trip_with_invariant_string()
    {
        await using var fx = new DbFixture();
        var sut = Svc(fx);
        await sut.SetEnumAsync("theme.mode", ThemeMode.Dark);
        var got = await sut.GetEnumAsync<ThemeMode>("theme.mode");
        got.Should().Be(ThemeMode.Dark);
    }

    [Fact]
    public async Task GetEnumAsync_returns_null_for_unparseable_value()
    {
        await using var fx = new DbFixture();
        var sut = Svc(fx);
        await sut.SetAsync("theme.mode", "banana");
        var got = await sut.GetEnumAsync<ThemeMode>("theme.mode");
        got.Should().BeNull();
    }
}
