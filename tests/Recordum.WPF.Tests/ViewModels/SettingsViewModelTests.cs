using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public async Task Initialize_reflects_current_theme_mode()
    {
        var theme = new FakeThemeService { CurrentMode = ThemeMode.Dark, EffectiveTheme = ThemeMode.Dark };
        var sut = new SettingsViewModel(theme, new FakeAppFontService());

        await sut.InitializeAsync();

        sut.ThemeMode.Should().Be(ThemeMode.Dark);
    }

    [Fact]
    public async Task Initialize_does_not_call_SetModeAsync()
    {
        var theme = new FakeThemeService { CurrentMode = ThemeMode.Light };
        var sut = new SettingsViewModel(theme, new FakeAppFontService());

        await sut.InitializeAsync();

        theme.SetModeCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_ThemeMode_calls_service()
    {
        var theme = new FakeThemeService();
        var sut = new SettingsViewModel(theme, new FakeAppFontService());
        await sut.InitializeAsync();

        sut.ThemeMode = ThemeMode.Light;
        await Task.Delay(10);

        theme.SetModeCalls.Should().Contain(ThemeMode.Light);
    }

}
