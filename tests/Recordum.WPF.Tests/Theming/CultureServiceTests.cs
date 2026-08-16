using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Theming;
using Xunit;

namespace Recordum.WPF.Tests.Theming;

/// <summary>F0-6 (selettore) — the two culture knobs persist independently: an app in English whose
/// dates and decimals stay Italian is the case that made them two knobs instead of one.</summary>
public class CultureServiceTests
{
    [Fact]
    public async Task Both_default_to_the_system_when_unset()
    {
        var sut = new CultureService(new FakeAppSettingService());

        await sut.InitializeAsync();

        sut.Language.Should().Be(AppCulture.System);
        sut.Format.Should().Be(AppCulture.System);
    }

    [Fact]
    public async Task Initialize_loads_both_persisted_choices()
    {
        var settings = new FakeAppSettingService();
        await settings.SetEnumAsync("ui.language", AppCulture.English);
        await settings.SetEnumAsync("ui.format", AppCulture.Italian);
        var sut = new CultureService(settings);

        await sut.InitializeAsync();

        sut.Language.Should().Be(AppCulture.English);
        sut.Format.Should().Be(AppCulture.Italian);
    }

    [Fact]
    public async Task Setting_the_language_leaves_the_format_alone()
    {
        var settings = new FakeAppSettingService();
        var sut = new CultureService(settings);
        await sut.InitializeAsync();

        await sut.SetLanguageAsync(AppCulture.English);

        (await settings.GetEnumAsync<AppCulture>("ui.language")).Should().Be(AppCulture.English);
        sut.Format.Should().Be(AppCulture.System);
        (await settings.GetEnumAsync<AppCulture>("ui.format")).Should().BeNull("an untouched knob is not persisted");
    }

    [Fact]
    public async Task Setting_the_format_leaves_the_language_alone()
    {
        var settings = new FakeAppSettingService();
        var sut = new CultureService(settings);
        await sut.InitializeAsync();

        await sut.SetFormatAsync(AppCulture.Italian);

        (await settings.GetEnumAsync<AppCulture>("ui.format")).Should().Be(AppCulture.Italian);
        sut.Language.Should().Be(AppCulture.System);
    }
}
