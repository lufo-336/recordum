using FluentAssertions;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Xunit;

namespace Recordum.Presentation.Tests.Services;

/// <summary>Le righe della Home a scelta: la preferenza <c>home.rows</c> e la Home che la segue senza riavvio.</summary>
public class HomeRowsPreferenceTests
{
    [Fact]
    public async Task Defaults_to_the_fallback()
    {
        var settings = new FakeAppSettingService();
        var sut = new HomeRowsPreference(settings, HomeRows.Compact);
        sut.Current.Should().Be(HomeRows.Compact, "prima di InitializeAsync vale la predefinita della shell");

        await sut.InitializeAsync();

        sut.Current.Should().Be(HomeRows.Compact);
        settings.SetCount.Should().Be(0, "nessun seed: la chiave si scrive al primo cambio");
    }

    [Fact]
    public async Task Reads_the_saved_value()
    {
        var settings = new FakeAppSettingService { Store = { ["home.rows"] = "Marks" } };
        var sut = new HomeRowsPreference(settings, HomeRows.Comfortable);

        await sut.InitializeAsync();

        sut.Current.Should().Be(HomeRows.Marks);
    }

    // Review Focus 5: un file di un'altra versione, o scritto a mano. "7" passa Enum.TryParse: non è una delle tre.
    [Theory]
    [InlineData("Gigantic")]
    [InlineData("7")]
    [InlineData("")]
    public async Task Unknown_value_falls_back_without_throwing(string saved)
    {
        var settings = new FakeAppSettingService { Store = { ["home.rows"] = saved } };
        var sut = new HomeRowsPreference(settings, HomeRows.Comfortable);

        await sut.Invoking(s => s.InitializeAsync().AsTask()).Should().NotThrowAsync();

        sut.Current.Should().Be(HomeRows.Comfortable);
    }

    [Fact]
    public async Task Set_persists_and_raises_changed_once()
    {
        var settings = new FakeAppSettingService();
        var sut = new HomeRowsPreference(settings, HomeRows.Comfortable);
        await sut.InitializeAsync();
        var raised = 0;
        sut.Changed += (_, _) => raised++;

        await sut.SetAsync(HomeRows.Marks);
        await sut.SetAsync(HomeRows.Marks);

        sut.Current.Should().Be(HomeRows.Marks);
        settings.Store["home.rows"].Should().Be("Marks");
        settings.SetCount.Should().Be(1);
        raised.Should().Be(1, "lo stesso valore due volte non è un cambio");
    }

    [Fact]
    public async Task Home_follows_the_preference_live()
    {
        var rows = new HomeRowsPreference(new FakeAppSettingService(), HomeRows.Compact);
        using var home = new HomeViewModel(new FakeHabitTreeQueryService(), new FakeHabitNodeService(), new FakePurgeService(), rows: rows);
        home.RowsStyle.Should().Be(HomeRows.Compact, "la Home nasce con la scelta corrente");
        var notified = 0;
        home.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(HomeViewModel.RowsStyle)) notified++; };

        await rows.SetAsync(HomeRows.Marks);

        home.RowsStyle.Should().Be(HomeRows.Marks);
        notified.Should().Be(1);
    }

    [Fact]
    public async Task A_disposed_home_stops_following()
    {
        var rows = new HomeRowsPreference(new FakeAppSettingService(), HomeRows.Comfortable);
        var home = new HomeViewModel(new FakeHabitTreeQueryService(), new FakeHabitNodeService(), new FakePurgeService(), rows: rows);
        home.Dispose();

        await rows.SetAsync(HomeRows.Marks);

        home.RowsStyle.Should().Be(HomeRows.Comfortable);
    }

    [Fact]
    public void Without_a_preference_the_home_is_comfortable()
    {
        // WPF (congelata) non registra la preferenza: lì la Home resta Larga.
        using var home = new HomeViewModel(new FakeHabitTreeQueryService(), new FakeHabitNodeService(), new FakePurgeService());
        home.RowsStyle.Should().Be(HomeRows.Comfortable);
    }
}
