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
            if (e.PropertyName is nameof(BackupSettingsViewModel.InertWarning) or nameof(BackupSettingsViewModel.SelfDisabled))
            {
                OnPropertyChanged(nameof(AutoBackupOffWarning));
                OnPropertyChanged(nameof(AutoBackupOffText));
            }
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

    /// <summary>Acceso senza passphrase leggibile, o spento dallo scheduler per lo stesso motivo (segnale
    /// <c>backup.auto.disabled_reason</c>): in entrambi i casi l'utente non deve credere che i backup girino.</summary>
    public bool AutoBackupOffWarning => Settings.InertWarning || Settings.SelfDisabled;

    public string AutoBackupOffText => Settings.SelfDisabled
        ? Strings.Backup_Settings_SelfDisabledWarning
        : Strings.Backup_Settings_InertWarning;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await Settings.LoadAsync(ct).ConfigureAwait(true);
        await History.LoadAsync(ct).ConfigureAwait(true);
        if (ShowDeviceKey) IsDeviceKeyRemembered = await Task.Run(ReadDeviceKeyState, ct).ConfigureAwait(true);
        OnPropertyChanged(nameof(AutoBackupOffWarning));
        OnPropertyChanged(nameof(AutoBackupOffText));
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
        // Riuscito, il database e' chiuso fino all'uscita: rileggere lo storico aspetterebbe per sempre.
        if (await _launcher.ShowRestoreAsync().ConfigureAwait(true)) return;
        await History.LoadAsync().ConfigureAwait(true);
    }

    // Una scrittura "ricorda" andata in timeout allo sblocco puo' essere ancora in volo: il portachiavi della shell mette
    // in fila le operazioni sullo stesso segreto (SerializedSecretStore, F2c-4), quindi questa cancellazione arriva dopo.
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
