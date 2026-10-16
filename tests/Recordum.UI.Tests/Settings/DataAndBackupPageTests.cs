using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.Presentation.ViewModels.Settings;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Settings;
using Xunit;

namespace Recordum.UI.Tests.Settings;

public class DataAndBackupPageTests
{
    private sealed class FakeSecrets(bool dbKey) : ISecretStore
    {
        public bool DbKeyPresent { get; private set; } = dbKey;
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) => kind == SecretKind.DbKey && DbKeyPresent ? "k" : null;
        public void Write(SecretKind kind, string value) { }
        public void Delete(SecretKind kind) { if (kind == SecretKind.DbKey) DbKeyPresent = false; }
    }

    private sealed class YesDialog(bool answer) : IConfirmationDialog
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(answer);
    }

    private sealed class RecordingLauncher : ISettingsWindowLauncher
    {
        public int Shown;
        public Task ShowAsync(SettingsCategoryId? initialCategory = null) { Shown++; return Task.CompletedTask; }
    }

    private static PrivateDbHost NewHost(string name, FakeSecrets? secrets = null, Action<IServiceCollection>? more = null) =>
        new(name, s =>
        {
            if (secrets is not null) s.AddSingleton<ISecretStore>(secrets);
            s.AddSingleton<IConfirmationDialog>(new YesDialog(true));
            more?.Invoke(s);
        });

    private static async Task<(SettingsWindow Window, DataAndBackupPageViewModel Vm)> OpenAsync(
        IServiceProvider scopeProvider, bool? showDeviceKey = null, ThemeVariant? variant = null, double width = 640)
    {
        var real = scopeProvider.GetRequiredService<DataAndBackupPageViewModel>();
        var vm = showDeviceKey is null || showDeviceKey == real.ShowDeviceKey ? real
            : new DataAndBackupPageViewModel(real.Settings, real.History, scopeProvider.GetRequiredService<IBackupDialogLauncher>(),
                scopeProvider.GetRequiredService<ISecretStore>(), scopeProvider.GetRequiredService<IConfirmationDialog>(), showDeviceKey.Value);
        await vm.LoadAsync();
        var window = new SettingsWindow { DataContext = vm, Width = width };
        if (variant is not null) window.RequestedThemeVariant = variant;
        window.Show();
        PrivateDbHost.Pump();
        return (window, vm);
    }

    private static T Find<T>(Window w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task Page_shows_the_four_parts_and_the_device_key_row_follows_the_composition()
    {
        var host = NewHost("set-parts", new FakeSecrets(true));
        await using var scope = host.Provider.CreateAsyncScope();
        var (window, _) = await OpenAsync(scope.ServiceProvider, showDeviceKey: true);
        try
        {
            Find<CheckBox>(window, "EnabledBox").IsVisible.Should().BeTrue();
            Find<Button>(window, "BackupNowButton").IsVisible.Should().BeTrue();
            Find<Button>(window, "RestoreButton").IsVisible.Should().BeTrue();
            Find<ItemsControl>(window, "HistoryList").IsVisible.Should().BeTrue();
            Find<StackPanel>(window, "DeviceKeyPanel").IsEffectivelyVisible.Should().BeTrue();
        }
        finally { window.Close(); }

        var (windows, _) = await OpenAsync(scope.ServiceProvider, showDeviceKey: false);
        try { Find<StackPanel>(windows, "DeviceKeyPanel").IsEffectivelyVisible.Should().BeFalse(); }
        finally { windows.Close(); }
    }

    [AvaloniaFact]
    public async Task Forget_turns_the_remembered_line_into_not_remembered_and_deletes_the_key()
    {
        var secrets = new FakeSecrets(true);
        var host = NewHost("set-forget", secrets);
        await using var scope = host.Provider.CreateAsyncScope();
        var (window, vm) = await OpenAsync(scope.ServiceProvider, showDeviceKey: true);
        try
        {
            Find<Button>(window, "ForgetButton").IsEnabled.Should().BeTrue();
            await vm.ForgetDeviceKeyCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            secrets.DbKeyPresent.Should().BeFalse();
            Find<TextBlock>(window, "DeviceKeyText").Text.Should().Be(Recordum.Presentation.Resources.Strings.Settings_DeviceKey_NotRemembered);
            Find<Button>(window, "ForgetButton").IsEnabled.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Off_warning_is_visible_when_enabled_without_a_readable_passphrase()
    {
        var host = NewHost("set-warn", new FakeSecrets(false));
        await using var scope = host.Provider.CreateAsyncScope();
        var (window, vm) = await OpenAsync(scope.ServiceProvider, showDeviceKey: true);
        try
        {
            vm.Settings.IsEnabled = false;
            PrivateDbHost.Pump();
            Find<TextBlock>(window, "OffWarning").IsVisible.Should().BeFalse();
            vm.Settings.IsEnabled = true;   // nessuna passphrase nel deposito di prova
            PrivateDbHost.Pump();
            Find<TextBlock>(window, "OffWarning").IsVisible.Should().BeTrue();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Passphrase_fields_have_accessible_names()
    {
        var host = NewHost("set-a11y", new FakeSecrets(false));
        await using var scope = host.Provider.CreateAsyncScope();
        var (window, _) = await OpenAsync(scope.ServiceProvider, showDeviceKey: true);
        try
        {
            AutomationProperties.GetName(Find<TextBox>(window, "PassphraseBox")).Should().Be(Recordum.Presentation.Resources.Strings.Backup_Create_Passphrase);
            AutomationProperties.GetName(Find<TextBox>(window, "ConfirmBox")).Should().Be(Recordum.Presentation.Resources.Strings.Backup_Create_Confirm);
            Find<TextBox>(window, "PassphraseBox").PasswordChar.Should().Be('●');
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task At_640_px_no_visible_control_leaves_the_window(string variant)
    {
        var host = NewHost("set-fit-" + variant, new FakeSecrets(true));
        await using var scope = host.Provider.CreateAsyncScope();
        var (window, _) = await OpenAsync(scope.ServiceProvider, showDeviceKey: true,
            variant: variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light);
        try
        {
            window.ClientSize.Width.Should().Be(640);
            var controls = window.GetVisualDescendants().Where(c => c is TextBlock or Button or TextBox or CheckBox or ItemsControl)
                .Cast<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0).ToList();
            controls.Should().NotBeEmpty();
            foreach (var c in controls)
            {
                var origin = c.TranslatePoint(default, window)!.Value;
                origin.X.Should().BeGreaterThanOrEqualTo(-0.5, $"{c.GetType().Name} {c.Name}");
                (origin.X + c.Bounds.Width).Should().BeLessThanOrEqualTo(640.5, $"{c.GetType().Name} {c.Name}");
            }
            ScreenshotProbeTests.Capture(window, "settings-data-backup-" + variant.ToLowerInvariant());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Rail_settings_button_is_visible_named_with_a_tooltip_and_opens_the_settings()
    {
        var launcher = new RecordingLauncher();
        var host = NewHost("set-rail", more: s => s.AddSingleton<ISettingsWindowLauncher>(launcher));
        var (window, _) = await ShellFixture.OpenMainWindowAsync(host.Provider.GetRequiredService<MainWindowViewModel>());
        try
        {
            var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SettingsButton");
            button.IsVisible.Should().BeTrue();
            AutomationProperties.GetName(button).Should().Be(Recordum.Presentation.Resources.Strings.Settings_Title);
            ToolTip.GetTip(button).Should().Be(Recordum.Presentation.Resources.Strings.Settings_Title);
            button.Command!.Execute(null);
            launcher.Shown.Should().Be(1);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_real_launcher_opens_the_settings_window_with_the_page_loaded()
    {
        var host = NewHost("set-launcher", new FakeSecrets(true));
        var launcher = new AvaloniaSettingsWindowLauncher(host.Provider, () => null);
        Window? created = null;
        launcher.WindowCreatedForTests += w => created = w;
        var shown = launcher.ShowAsync();
        for (var i = 0; i < 200 && created is null; i++) { await Task.Delay(25); PrivateDbHost.Pump(); }
        created.Should().BeOfType<SettingsWindow>();
        created!.Close();
        await shown;
    }

    [AvaloniaFact]
    public async Task Backup_now_from_the_modal_settings_window_opens_its_dialog_owned_by_the_settings_window()
    {
        var host = NewHost("set-owner", new FakeSecrets(true));
        var main = new Window { Width = 800, Height = 600 };
        main.Show();
        PrivateDbHost.Pump();
        var settings = new AvaloniaSettingsWindowLauncher(host.Provider, () => main);
        Window? settingsWindow = null;
        settings.WindowCreatedForTests += w => settingsWindow = w;
        var backup = new AvaloniaBackupDialogLauncher(host.Provider, () => Recordum.UI.Bootstrap.AvaloniaShellServices.Topmost(main));
        Window? dialog = null;
        backup.WindowCreatedForTests += w => dialog = w;
        Task? settingsShown = null, backupShown = null;
        try
        {
            settingsShown = settings.ShowAsync();
            for (var i = 0; i < 200 && settingsWindow is null; i++) { await Task.Delay(25); PrivateDbHost.Pump(); }
            settingsWindow.Should().NotBeNull();
            settingsWindow!.Owner.Should().BeSameAs(main);

            backupShown = backup.ShowBackupAsync();
            for (var i = 0; i < 200 && dialog is null; i++) { await Task.Delay(25); PrivateDbHost.Pump(); }
            dialog.Should().NotBeNull();
            dialog!.Owner.Should().BeSameAs(settingsWindow, "la dialog sta davanti alla finestra in cui l'utente si trova");
        }
        finally
        {
            dialog?.Close();
            if (backupShown is not null) await backupShown;
            settingsWindow?.Close();
            if (settingsShown is not null) await settingsShown;
            main.Close();
        }
    }
}
