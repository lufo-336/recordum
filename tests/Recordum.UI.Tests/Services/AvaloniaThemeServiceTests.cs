using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Recordum.Core.Enums;
using Recordum.UI.Services;
using Xunit;
using FluentAssertions;

namespace Recordum.UI.Tests.Services;

/// <summary>Gemello dei test WPF di <c>ThemeServiceTests</c> (stesso `AppSetting`, stesso contratto
/// <see cref="Recordum.Core.Abstractions.Theming.IThemeService"/>), ma qui il "sistema" non è un fake
/// Func&lt;bool&gt; — è Avalonia stesso: impostare <see cref="ThemeVariant.Default"/> risolve da sé
/// nell'headless (misurato: Light).</summary>
public class AvaloniaThemeServiceTests
{
    [AvaloniaFact]
    public async Task Initialize_loads_the_persisted_mode_from_AppSetting()
    {
        var settings = new FakeAppSettingService();
        await settings.SetEnumAsync("theme.mode", ThemeMode.Dark);
        var sut = new AvaloniaThemeService(settings, Application.Current!);

        await sut.InitializeAsync();

        sut.CurrentMode.Should().Be(ThemeMode.Dark);
        sut.EffectiveTheme.Should().Be(ThemeMode.Dark);
        Application.Current!.RequestedThemeVariant.Should().Be(ThemeVariant.Dark);
    }

    [AvaloniaFact]
    public async Task SetMode_persists_via_AppSettingService_and_applies_the_variant()
    {
        var settings = new FakeAppSettingService();
        var sut = new AvaloniaThemeService(settings, Application.Current!);
        await sut.InitializeAsync();

        await sut.SetModeAsync(ThemeMode.Light);

        (await settings.GetEnumAsync<ThemeMode>("theme.mode")).Should().Be(ThemeMode.Light);
        Application.Current!.RequestedThemeVariant.Should().Be(ThemeVariant.Light);
        sut.EffectiveTheme.Should().Be(ThemeMode.Light);
    }

    [AvaloniaFact]
    public async Task System_mode_maps_to_ThemeVariant_Default_and_resolves_through_Avalonia()
    {
        var settings = new FakeAppSettingService();
        var sut = new AvaloniaThemeService(settings, Application.Current!);
        await sut.InitializeAsync();

        // Esercita la transizione: Dark → System (non solo Initialize con System già il default)
        await sut.SetModeAsync(ThemeMode.Dark);
        await sut.SetModeAsync(ThemeMode.System);

        Application.Current!.RequestedThemeVariant.Should().Be(ThemeVariant.Default);
        // L'headless risolve System a Light (misurato in sonda): non un ipotesi, un fatto di questo
        // ambiente di test — EffectiveTheme deve leggerlo da Avalonia, non ricalcolarlo da sé.
        sut.EffectiveTheme.Should().Be(ThemeMode.Light);
    }

    [AvaloniaFact]
    public async Task EffectiveThemeChanged_fires_when_the_actual_variant_changes()
    {
        var settings = new FakeAppSettingService();
        var sut = new AvaloniaThemeService(settings, Application.Current!);
        await sut.InitializeAsync();

        var raised = new List<ThemeMode>();
        sut.EffectiveThemeChanged += (_, mode) => raised.Add(mode);

        await sut.SetModeAsync(ThemeMode.Dark);
        await sut.SetModeAsync(ThemeMode.Light);

        raised.Should().Equal(ThemeMode.Dark, ThemeMode.Light);
    }

    [AvaloniaFact]
    public async Task Disposing_stops_listening_to_ActualThemeVariantChanged()
    {
        var settings = new FakeAppSettingService();
        var sut = new AvaloniaThemeService(settings, Application.Current!);
        await sut.InitializeAsync();
        var raised = 0;
        sut.EffectiveThemeChanged += (_, _) => raised++;

        sut.Dispose();
        await sut.SetModeAsync(ThemeMode.Dark); // scrive comunque su RequestedThemeVariant

        raised.Should().Be(0, "un servizio disposto non deve più notificare");
    }
}
