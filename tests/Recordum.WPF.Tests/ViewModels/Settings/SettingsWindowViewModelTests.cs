using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.Theming;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.DiagnosticInfo;
using Recordum.Presentation.ViewModels.Settings;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Settings;

public class SettingsWindowViewModelTests
{
    private static SettingsWindowViewModel CreateSut()
        => new(
            new SettingsViewModel(new FakeThemeService(), new FakeAppFontService()),
            new GeneralSettingsViewModel(new FakeAppSettingService(), new CultureService(new FakeAppSettingService()), new FakeClock()),
            new BackupSettingsViewModel(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), new FakeBackupSchedulerRunner()),
            new DiagnosticInfoViewModel(
                new FakeAppSettingService(), new FakeThemeService(), new FakeWebView2Availability(available: false),
                new FakeBackupStoragePaths(), new FakeBackupLogService(), new FakePluginManagerDiag(), new FakeClipboardDiag()));

    [Fact]
    public void Categories_are_seven_in_canonical_order_with_future_ones_disabled()
    {
        var sut = CreateSut();

        sut.Categories.Select(c => c.Id).Should().Equal(
            SettingsCategoryId.Appearance, SettingsCategoryId.General, SettingsCategoryId.Backup,
            SettingsCategoryId.Diagnostics, SettingsCategoryId.Sync, SettingsCategoryId.Plugins,
            SettingsCategoryId.Ai);
        sut.Categories.Where(c => !c.IsEnabled).Select(c => c.Id).Should().Equal(
            SettingsCategoryId.Sync, SettingsCategoryId.Plugins, SettingsCategoryId.Ai);
        sut.Categories.Where(c => !c.IsEnabled).Should().OnlyContain(c => c.Content is ComingSoonContent);
    }

    [Fact]
    public void Default_selection_is_appearance()
    {
        var sut = CreateSut();
        sut.SelectInitial(null);
        sut.SelectedCategory!.Id.Should().Be(SettingsCategoryId.Appearance);
    }

    [Fact]
    public void Initial_category_request_is_honoured()
    {
        var sut = CreateSut();
        sut.SelectInitial(SettingsCategoryId.Backup);
        sut.SelectedCategory!.Id.Should().Be(SettingsCategoryId.Backup);
    }

    [Fact]
    public void Initial_request_for_disabled_category_falls_back_to_appearance()
    {
        var sut = CreateSut();
        sut.SelectInitial(SettingsCategoryId.Sync);
        sut.SelectedCategory!.Id.Should().Be(SettingsCategoryId.Appearance);
    }
}
