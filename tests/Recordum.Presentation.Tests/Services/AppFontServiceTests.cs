using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.Services;

public class AppFontServiceTests
{
    [Fact]
    public async Task Default_is_Serif_when_unset()
    {
        var sut = new AppFontService(new FakeAppSettingService());

        await sut.InitializeAsync();

        sut.CurrentFont.Should().Be(AppFont.Serif);  // Palatino is the shipped default
    }

    [Fact]
    public async Task Initialize_loads_persisted_font()
    {
        var settings = new FakeAppSettingService();
        await settings.SetEnumAsync("ui.font", AppFont.System);
        var sut = new AppFontService(settings);

        await sut.InitializeAsync();

        sut.CurrentFont.Should().Be(AppFont.System);
    }

    [Fact]
    public async Task SetFont_persists_via_AppSettingService()
    {
        var settings = new FakeAppSettingService();
        var sut = new AppFontService(settings);
        await sut.InitializeAsync();

        await sut.SetFontAsync(AppFont.System);

        (await settings.GetEnumAsync<AppFont>("ui.font")).Should().Be(AppFont.System);
    }

    [Fact]
    public async Task FontChanged_fires_on_actual_change_only()
    {
        var sut = new AppFontService(new FakeAppSettingService());
        await sut.InitializeAsync();  // default Serif

        var raised = 0;
        sut.FontChanged += (_, _) => raised++;

        await sut.SetFontAsync(AppFont.System);  // Serif -> System: fires
        await sut.SetFontAsync(AppFont.System);  // no change
        await sut.SetFontAsync(AppFont.Serif);   // System -> Serif: fires
        raised.Should().Be(2);
    }
}
