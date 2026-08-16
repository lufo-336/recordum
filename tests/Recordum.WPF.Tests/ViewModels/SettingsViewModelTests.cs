using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Theming;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static CultureService NewCultureService() => new(new FakeAppSettingService());

    [Fact]
    public async Task Initialize_reflects_current_theme_mode()
    {
        var theme = new FakeThemeService { CurrentMode = ThemeMode.Dark, EffectiveTheme = ThemeMode.Dark };
        var sut = new SettingsViewModel(theme, new FakeAppFontService(), NewCultureService());

        await sut.InitializeAsync();

        sut.ThemeMode.Should().Be(ThemeMode.Dark);
    }

    [Fact]
    public async Task Initialize_does_not_call_SetModeAsync()
    {
        var theme = new FakeThemeService { CurrentMode = ThemeMode.Light };
        var sut = new SettingsViewModel(theme, new FakeAppFontService(), NewCultureService());

        await sut.InitializeAsync();

        theme.SetModeCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_ThemeMode_calls_service()
    {
        var theme = new FakeThemeService();
        var sut = new SettingsViewModel(theme, new FakeAppFontService(), NewCultureService());
        await sut.InitializeAsync();

        sut.ThemeMode = ThemeMode.Light;
        await Task.Delay(10);

        theme.SetModeCalls.Should().Contain(ThemeMode.Light);
    }

    /// <summary>F0-6 — the two culture knobs are independent, and neither is written while the screen
    /// is loading (the guard that keeps opening Settings from persisting a choice nobody made).</summary>
    [Fact]
    public async Task Initialize_reflects_the_persisted_cultures_without_writing_them()
    {
        var settings = new FakeAppSettingService();
        await settings.SetEnumAsync("ui.language", AppCulture.English);
        var cultures = new CultureService(settings);
        await cultures.InitializeAsync();
        var sut = new SettingsViewModel(new FakeThemeService(), new FakeAppFontService(), cultures);

        await sut.InitializeAsync();

        sut.UiLanguage.Should().Be(AppCulture.English);
        sut.RegionalFormat.Should().Be(AppCulture.System);
        (await settings.GetEnumAsync<AppCulture>("ui.format")).Should().BeNull();
    }

    [Fact]
    public async Task Changing_the_language_leaves_the_format_untouched()
    {
        var cultures = NewCultureService();
        var sut = new SettingsViewModel(new FakeThemeService(), new FakeAppFontService(), cultures);
        await sut.InitializeAsync();

        sut.UiLanguage = AppCulture.English;

        cultures.Language.Should().Be(AppCulture.English);
        cultures.Format.Should().Be(AppCulture.System);
    }

    [Fact]
    public async Task Changing_the_format_leaves_the_language_untouched()
    {
        var cultures = NewCultureService();
        var sut = new SettingsViewModel(new FakeThemeService(), new FakeAppFontService(), cultures);
        await sut.InitializeAsync();

        sut.RegionalFormat = AppCulture.Italian;

        cultures.Format.Should().Be(AppCulture.Italian);
        cultures.Language.Should().Be(AppCulture.System);
    }
}
