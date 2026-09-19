using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Theming;
using Recordum.Presentation.ViewModels.Settings;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Settings;

public class GeneralSettingsViewModelTests
{
    private readonly FakeAppSettingService _settings = new();

    [Fact]
    public async Task LoadAsync_reads_daily_reset_time()
    {
        await _settings.SetAsync("daily_reset_time", "05:30");
        var sut = new GeneralSettingsViewModel(_settings, new CultureService(new FakeAppSettingService()), new FakeClock());

        await sut.LoadAsync();

        sut.DailyResetTime.Should().Be("05:30");
    }

    [Fact]
    public async Task Valid_time_is_persisted_on_change()
    {
        var sut = new GeneralSettingsViewModel(_settings, new CultureService(new FakeAppSettingService()), new FakeClock());
        await sut.LoadAsync();

        sut.DailyResetTime = "06:15";

        (await _settings.GetAsync("daily_reset_time")).Should().Be("06:15");
    }

    [Fact]
    public async Task Invalid_time_is_not_persisted()
    {
        await _settings.SetAsync("daily_reset_time", "00:00");
        var sut = new GeneralSettingsViewModel(_settings, new CultureService(new FakeAppSettingService()), new FakeClock());
        await sut.LoadAsync();

        sut.DailyResetTime = "25:99";

        (await _settings.GetAsync("daily_reset_time")).Should().Be("00:00");
        sut.HasError.Should().BeTrue();
    }
}
