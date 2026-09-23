using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.DiagnosticInfo;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.WPF.Views.Settings;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

/// <summary>F0-6 — the two culture knobs against the real compiled <c>SettingsWindow.xaml</c>. A
/// ViewModel test cannot see this surface: the radio groups live inside a DataTemplate, so a typo in
/// an <c>x:Static</c> or a missing resource would surface only when a user opens Settings. They sit
/// under <b>General</b>, not Appearance: culture changes behaviour, not looks.</summary>
public sealed class SettingsCultureViewTests
{
    private static SettingsWindowViewModel NewVm() => new(
        new SettingsViewModel(new FakeThemeService(), new FakeAppFontService()),
        new GeneralSettingsViewModel(new FakeAppSettingService(), new CultureService(new FakeAppSettingService()), new FakeClock()),
        new BackupSettingsViewModel(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), new FakeBackupSchedulerRunner()),
        new DiagnosticInfoViewModel(
            new FakeAppSettingService(), new FakeThemeService(), new FakeWebView2Availability(available: false),
            new FakeBackupStoragePaths(), new FakeBackupLogService(), new FakePluginManagerDiag(), new FakeClipboardDiag()));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    [WpfFact]
    public void General_offers_a_language_and_a_regional_format_group()
    {
        TestApp.EnsureLoaded();
        var vm = NewVm();
        vm.SelectInitial(SettingsCategoryId.General);
        var window = new SettingsWindow(vm)
        {
            Width = 900,
            Height = 700,
            // Shown off-screen: a Window builds no visual tree until it has an HWND, and Measure on an
            // unshown one leaves the DataTemplate unmaterialized — which is the whole point here.
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000,
            ShowInTaskbar = false,
        };
        window.Show();
        window.UpdateLayout();

        var groups = Descendants<RadioButton>(window)
            .GroupBy(r => r.GroupName)
            .ToDictionary(g => g.Key, g => g.Count());
        window.Close();

        groups.Should().ContainKey("Language").WhoseValue.Should().Be(3, "sistema, italiano, inglese");
        groups.Should().ContainKey("Format").WhoseValue.Should().Be(3);
    }
}
