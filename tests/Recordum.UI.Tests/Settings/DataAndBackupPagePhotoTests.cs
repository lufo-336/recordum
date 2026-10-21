using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Settings;
using Xunit;

namespace Recordum.UI.Tests.Settings;

public class DataAndBackupPagePhotoTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-photos-page");

    private static readonly string[] PhotoRadios = ["PhotoModeResizeAndClean", "PhotoModeCleanOnly", "PhotoModeKeepOriginal"];

    private static T Find<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task The_photo_section_has_three_choices_and_the_gps_warning_only_for_the_original()
    {
        using var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<DataAndBackupPageViewModel>();
        await vm.LoadAsync();
        var window = new Window { Width = 640, Height = 900, Content = new DataAndBackupPage { DataContext = vm } };
        window.Show();
        PrivateDbHost.Pump();
        try
        {
            var radios = PhotoRadios
                .Select(n => Find<RadioButton>(window, n)).ToList();
            radios.Select(r => r.IsChecked == true).Should().Equal([true, false, false], "il predefinito e' Ridimensiona e pulisci");
            Find<TextBlock>(window, "PhotoGpsWarning").IsEffectivelyVisible.Should().BeFalse();

            radios[2].IsChecked = true;
            PrivateDbHost.Pump();
            vm.Photos.Mode.Should().Be(PhotoImportMode.KeepOriginal);
            Find<TextBlock>(window, "PhotoGpsWarning").IsEffectivelyVisible.Should().BeTrue();

            radios[1].IsChecked = true;
            PrivateDbHost.Pump();
            vm.Photos.Mode.Should().Be(PhotoImportMode.CleanOnly);
            Find<TextBlock>(window, "PhotoGpsWarning").IsEffectivelyVisible.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_failed_save_of_the_setting_is_shown_on_the_page()
    {
        using var scope = Host.Provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<DataAndBackupPageViewModel>();
        await vm.LoadAsync();
        var window = new Window { Width = 640, Height = 900, Content = new DataAndBackupPage { DataContext = vm } };
        window.Show();
        try
        {
            Find<TextBlock>(window, "PhotoError").IsEffectivelyVisible.Should().BeFalse();
            vm.Photos.ErrorMessage = "boom";
            PrivateDbHost.Pump();
            var error = Find<TextBlock>(window, "PhotoError");
            error.IsEffectivelyVisible.Should().BeTrue();
            error.Text.Should().Be("boom");
        }
        finally { window.Close(); }
    }
}
