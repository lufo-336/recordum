using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

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

    // Marks, non la predefinita Comfortable né default(HomeRows) = Compact: un HomeRows che non legge risponde diverso.
    [Fact]
    public async Task Home_rows_loads_from_the_preference()
    {
        var settings = new FakeAppSettingService { Store = { ["home.rows"] = "Marks" } };
        var rows = new HomeRowsPreference(settings, HomeRows.Comfortable);
        await rows.InitializeAsync();
        var sut = new SettingsViewModel(new FakeThemeService(), new FakeAppFontService(), rows);

        await sut.InitializeAsync();

        sut.HomeRows.Should().Be(HomeRows.Marks);
    }

    [Fact]
    public async Task Changing_home_rows_saves_once_and_not_while_loading()
    {
        var settings = new FakeAppSettingService { Store = { ["home.rows"] = "Compact" } };
        var rows = new HomeRowsPreference(settings, HomeRows.Comfortable);
        await rows.InitializeAsync();
        var sut = new SettingsViewModel(new FakeThemeService(), new FakeAppFontService(), rows);

        await sut.InitializeAsync();
        settings.SetCount.Should().Be(0, "leggere la scelta non la riscrive");

        sut.HomeRows = HomeRows.Marks;

        settings.SetCount.Should().Be(1);
        settings.Store["home.rows"].Should().Be("Marks");
        rows.Current.Should().Be(HomeRows.Marks);
    }

    [Fact]
    public async Task Without_a_preference_home_rows_is_comfortable_and_writes_nothing()
    {
        var sut = new SettingsViewModel(new FakeThemeService(), new FakeAppFontService());

        await sut.InitializeAsync();
        sut.HomeRows.Should().Be(HomeRows.Comfortable);

        sut.Invoking(s => s.HomeRows = HomeRows.Marks).Should().NotThrow();
    }
}
