using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Backup;

public class PhotoImportSettingsViewModelTests
{
    [Fact]
    public async Task Load_reads_the_saved_mode()
    {
        var settings = new FakeAppSettingService();
        settings.Store[PhotoImportSettings.Key] = nameof(PhotoImportMode.CleanOnly);
        var sut = new PhotoImportSettingsViewModel(settings);

        await sut.LoadAsync();

        sut.Mode.Should().Be(PhotoImportMode.CleanOnly);
        settings.SetCount.Should().Be(0, "caricare non è cambiare");
    }

    [Fact]
    public async Task No_saved_value_means_resize_and_clean()
    {
        var sut = new PhotoImportSettingsViewModel(new FakeAppSettingService());
        await sut.LoadAsync();
        sut.Mode.Should().Be(PhotoImportMode.ResizeAndClean);
    }

    [Fact]
    public async Task Changing_the_mode_saves_it_and_only_keep_original_warns_about_gps()
    {
        var settings = new FakeAppSettingService();
        var sut = new PhotoImportSettingsViewModel(settings);
        await sut.LoadAsync();
        sut.ShowsGpsWarning.Should().BeFalse();

        sut.Mode = PhotoImportMode.KeepOriginal;

        settings.Store[PhotoImportSettings.Key].Should().Be(nameof(PhotoImportMode.KeepOriginal));
        sut.ShowsGpsWarning.Should().BeTrue();
        sut.Mode = PhotoImportMode.CleanOnly;
        sut.ShowsGpsWarning.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_save_is_visible()
    {
        var settings = new FakeAppSettingService();
        var sut = new PhotoImportSettingsViewModel(settings);
        await sut.LoadAsync();
        settings.ThrowOnSet = new InvalidOperationException("locked");

        sut.Mode = PhotoImportMode.CleanOnly;

        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
