using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Backup;

/// <summary>L'unica pagina della finestra Impostazioni ridotta di Avalonia: backup automatico, "Backup ora" /
/// "Ripristina...", storico e (fuori da Windows) la riga della chiave del dispositivo. <paramref name="showDeviceKey"/>
/// lo decide la composizione, mai il ViewModel.</summary>
public sealed partial class DataAndBackupPageViewModel : ObservableObject
{
    private readonly IBackupDialogLauncher _launcher;
    private readonly ISecretStore _secrets;
    private readonly IConfirmationDialog _confirm;

    public DataAndBackupPageViewModel(
        BackupSettingsViewModel settings,
        BackupHistoryViewModel history,
        IBackupDialogLauncher launcher,
        ISecretStore secrets,
        IConfirmationDialog confirm,
        bool showDeviceKey)
    {
        Settings = settings;
        History = history;
        _launcher = launcher;
        _secrets = secrets;
        _confirm = confirm;
        ShowDeviceKey = showDeviceKey;
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BackupSettingsViewModel.InertWarning)) OnPropertyChanged(nameof(AutoBackupOffWarning));
        };
    }

    public BackupSettingsViewModel Settings { get; }
    public BackupHistoryViewModel History { get; }
    public bool ShowDeviceKey { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceKeyText))]
    private bool _isDeviceKeyRemembered;

    [ObservableProperty] private string? _deviceKeyError;

    public string DeviceKeyText => IsDeviceKeyRemembered
        ? Strings.Settings_DeviceKey_Remembered
        : Strings.Settings_DeviceKey_NotRemembered;

    /// <summary>Acceso nelle impostazioni ma senza passphrase leggibile: non gira finche' non se ne da' una. Lo scheduler,
    /// che in quel caso si spegne da solo, riscrive "spento" e la pagina lo mostra spento.</summary>
    public bool AutoBackupOffWarning => Settings.InertWarning;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await Settings.LoadAsync(ct).ConfigureAwait(true);
        await History.LoadAsync(ct).ConfigureAwait(true);
        if (ShowDeviceKey) IsDeviceKeyRemembered = await Task.Run(ReadDeviceKeyState, ct).ConfigureAwait(true);
        OnPropertyChanged(nameof(AutoBackupOffWarning));
    }

    // Fuori dal thread dell'interfaccia: IsAvailable e TryRead di libsecret possono attendere un prompt di sblocco.
    private bool ReadDeviceKeyState()
    {
        try { return _secrets.IsAvailable && _secrets.TryRead(SecretKind.DbKey) is not null; }
        catch (SecretStoreUnavailableException) { return false; }
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        await _launcher.ShowBackupAsync().ConfigureAwait(true);
        await History.LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        await _launcher.ShowRestoreAsync().ConfigureAwait(true);
        await History.LoadAsync().ConfigureAwait(true);
    }

    // Nota: una scrittura "ricorda" andata in timeout allo sblocco puo' completarsi piu' tardi, se l'utente risponde al
    // prompt del portachiavi, e annullare un "Dimentica" fatto nel frattempo qui. Il caso e' noto e non e' corretto.
    [RelayCommand]
    private async Task ForgetDeviceKeyAsync()
    {
        if (!IsDeviceKeyRemembered) return;   // ExecuteAsync non consulta CanExecute
        if (!await _confirm.ConfirmAsync(Strings.Settings_DeviceKey_Forget, Strings.Settings_DeviceKey_ForgetConfirm).ConfigureAwait(true)) return;
        try
        {
            await Task.Run(() => _secrets.Delete(SecretKind.DbKey)).ConfigureAwait(true);
            IsDeviceKeyRemembered = false;
            DeviceKeyError = null;
        }
        catch (SecretStoreUnavailableException)
        {
            DeviceKeyError = Strings.Unlock_RememberUnavailable;   // la chiave e' ancora li': lo stato non cambia
        }
    }
}
