using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task Initialize_calls_HomeViewModel_LoadCommand_and_SettingsViewModel_InitializeAsync()
    {
        var query = new FakeHabitTreeQueryService();
        var nodes = new FakeHabitNodeService();
        var purge = new FakePurgeService();
        var theme = new FakeThemeService { CurrentMode = ThemeMode.Dark };
        var home = new HomeViewModel(query, nodes, purge);
        var settings = new SettingsViewModel(theme);
        var sut = new MainWindowViewModel(home, settings);

        await sut.InitializeAsync();

        home.State.Should().NotBe(HomeState.Loading);
        settings.ThemeMode.Should().Be(ThemeMode.Dark);
        query.HomeCallCount.Should().Be(1);
    }

    [Fact]
    public void Home_and_Settings_are_exposed_to_the_view()
    {
        var query = new FakeHabitTreeQueryService();
        var home = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService());
        var settings = new SettingsViewModel(new FakeThemeService());
        var sut = new MainWindowViewModel(home, settings);

        sut.Home.Should().BeSameAs(home);
        sut.Settings.Should().BeSameAs(settings);
    }
}
