using System.Globalization;
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Theming;
using Recordum.Presentation.ViewModels.Settings;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Settings;

/// <summary>F0-6 — the culture knobs live in General, not Appearance: they change how the app behaves,
/// not how it looks. And the format choice is explained by a worked example rather than by a cryptic
/// "2,5" in a radio label — the preview says which date and which decimal you will actually get.</summary>
public class GeneralSettingsCultureTests
{
    [Fact]
    public async Task Load_reflects_the_persisted_cultures()
    {
        var store = new FakeAppSettingService();
        await store.SetEnumAsync("ui.language", AppCulture.English);
        var cultures = new CultureService(store);
        var sut = new GeneralSettingsViewModel(new FakeAppSettingService(), cultures, new FakeClock());

        await sut.LoadAsync();

        sut.UiLanguage.Should().Be(AppCulture.English);
        sut.RegionalFormat.Should().Be(AppCulture.System);
    }

    [Fact]
    public async Task Load_does_not_persist_a_choice_nobody_made()
    {
        var store = new FakeAppSettingService();
        var sut = new GeneralSettingsViewModel(new FakeAppSettingService(), new CultureService(store), new FakeClock());

        await sut.LoadAsync();

        (await store.GetEnumAsync<AppCulture>("ui.language")).Should().BeNull();
        (await store.GetEnumAsync<AppCulture>("ui.format")).Should().BeNull();
    }

    [Fact]
    public async Task The_two_knobs_are_independent()
    {
        var cultures = new CultureService(new FakeAppSettingService());
        var sut = new GeneralSettingsViewModel(new FakeAppSettingService(), cultures, new FakeClock());
        await sut.LoadAsync();

        sut.UiLanguage = AppCulture.English;

        cultures.Language.Should().Be(AppCulture.English);
        cultures.Format.Should().Be(AppCulture.System);
    }

    /// <summary>The preview is the answer to "2,5 meaning what?": it names the date and the decimal
    /// number, formatted in the culture being chosen, and it follows the radio button live.</summary>
    [Fact]
    public async Task The_preview_shows_a_real_date_and_a_real_decimal_in_the_chosen_culture()
    {
        var clock = new FakeClock();
        var today = clock.UtcNow.UtcDateTime;
        var sut = new GeneralSettingsViewModel(
            new FakeAppSettingService(), new CultureService(new FakeAppSettingService()), clock);
        await sut.LoadAsync();

        sut.RegionalFormat = AppCulture.Italian;
        var italian = sut.FormatPreview;

        sut.RegionalFormat = AppCulture.English;
        var english = sut.FormatPreview;

        italian.Should().Contain(today.ToString("d", CultureInfo.GetCultureInfo("it-IT")))
            .And.Contain("2,5");
        english.Should().Contain(today.ToString("d", CultureInfo.GetCultureInfo("en-US")))
            .And.Contain("2.5");
    }
}
