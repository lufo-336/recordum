using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Theming;
using Xunit;

namespace Recordum.WPF.Tests.Theming;

public class ThemeServiceTests
{
    [Fact]
    public async Task Initialize_loads_persisted_mode_from_AppSetting()
    {
        var settings = new FakeAppSettingService();
        await settings.SetEnumAsync("theme.mode", ThemeMode.Dark);
        var sut = new ThemeService(settings, NullLogger<ThemeService>.Instance, systemIsLight: () => true);

        await sut.InitializeAsync();

        sut.CurrentMode.Should().Be(ThemeMode.Dark);
        sut.EffectiveTheme.Should().Be(ThemeMode.Dark);
    }

    [Fact]
    public async Task When_mode_is_System_effective_follows_OS()
    {
        var settings = new FakeAppSettingService();
        var systemLight = true;
        var sut = new ThemeService(settings, NullLogger<ThemeService>.Instance, systemIsLight: () => systemLight);

        await sut.InitializeAsync();
        await sut.SetModeAsync(ThemeMode.System);
        sut.EffectiveTheme.Should().Be(ThemeMode.Light);

        systemLight = false;
        sut.OnSystemPreferenceChanged();
        sut.EffectiveTheme.Should().Be(ThemeMode.Dark);
    }

    [Fact]
    public async Task SetMode_persists_via_AppSettingService()
    {
        var settings = new FakeAppSettingService();
        var sut = new ThemeService(settings, NullLogger<ThemeService>.Instance, systemIsLight: () => true);
        await sut.InitializeAsync();

        await sut.SetModeAsync(ThemeMode.Light);

        (await settings.GetEnumAsync<ThemeMode>("theme.mode")).Should().Be(ThemeMode.Light);
    }

    [Fact]
    public async Task EffectiveThemeChanged_fires_on_actual_change_only()
    {
        var settings = new FakeAppSettingService();
        var sut = new ThemeService(settings, NullLogger<ThemeService>.Instance, systemIsLight: () => true);
        await sut.InitializeAsync();

        var raised = 0;
        sut.EffectiveThemeChanged += (_, _) => raised++;

        await sut.SetModeAsync(ThemeMode.Light);
        await sut.SetModeAsync(ThemeMode.Dark);
        await sut.SetModeAsync(ThemeMode.Dark);
        raised.Should().Be(1);
    }
}
