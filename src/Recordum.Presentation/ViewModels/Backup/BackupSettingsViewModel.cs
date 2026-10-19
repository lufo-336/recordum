using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Backup;

public interface IBackupSchedulerRunner
{
    Task RunNowAsync(CancellationToken ct = default);
}

public sealed partial class BackupSettingsViewModel(
    IAppSettingService settings,
    IAutoBackupKeyStore keyStore,
    IBackupSchedulerRunner runner,
    IFolderPicker folderPicker,
    IBackupStoragePaths paths) : ObservableObject
{
    /// <summary>La cartella che lo scheduler usa quando quella dell'utente e' vuota: la pagina la mostra come suggerimento.</summary>
    public string DefaultFolder => paths.BackupsDirectory;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private int _frequencyDays = 7;
    [ObservableProperty] private int _retentionCount = 8;
    [ObservableProperty] private string _folder = string.Empty;
    [ObservableProperty] private string _newPassphrase = string.Empty;
    [ObservableProperty] private string _confirmPassphrase = string.Empty;
    [ObservableProperty] private bool _hasPassphrase;
    [ObservableProperty] private string? _testResultMessage;
    /// <summary>Lo scheduler ha spento il backup automatico da solo (passphrase illeggibile): vale finche' l'utente non
    /// lo riaccende con una passphrase salvata. Vuoto = nessun segnale.</summary>
    [ObservableProperty] private bool _selfDisabled;

    private const string DisabledReasonKey = Recordum.Data.Backup.BackupScheduler.DisabledReasonKey;

    public bool InertWarning => IsEnabled && !HasPassphrase && string.IsNullOrEmpty(NewPassphrase);

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsEnabled = (await settings.GetAsync("backup.auto.enabled", ct).ConfigureAwait(true)) == "true";
        if (int.TryParse(await settings.GetAsync("backup.auto.frequency_days", ct).ConfigureAwait(true), out var f)) FrequencyDays = f;
        if (int.TryParse(await settings.GetAsync("backup.auto.retention_count", ct).ConfigureAwait(true), out var r)) RetentionCount = r;
        Folder = await settings.GetAsync("backup.auto.path", ct).ConfigureAwait(true) ?? string.Empty;
        // Il portachiavi di Linux e' una chiamata sincrona che puo' attendere un prompt di sblocco: mai sul thread dell'interfaccia (R6).
        // Portachiavi irraggiungibile: la pagina si apre lo stesso (la passphrase risulta "non salvata").
        try { HasPassphrase = await Task.Run(keyStore.HasKey, ct).ConfigureAwait(true); }
        catch (SecretStoreUnavailableException) { HasPassphrase = false; }
        SelfDisabled = !string.IsNullOrEmpty(await settings.GetAsync(DisabledReasonKey, ct).ConfigureAwait(true));
        OnPropertyChanged(nameof(InertWarning));
    }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(InertWarning));
    partial void OnNewPassphraseChanged(string value) => OnPropertyChanged(nameof(InertWarning));

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        if (await folderPicker.PickFolderAsync(string.IsNullOrWhiteSpace(Folder) ? DefaultFolder : Folder).ConfigureAwait(true) is { } picked)
            Folder = picked;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!string.IsNullOrEmpty(NewPassphrase))
        {
            if (NewPassphrase != ConfirmPassphrase) { TestResultMessage = Strings.Backup_Settings_PassphraseMismatch; return; }
            if (NewPassphrase.Length < 12) { TestResultMessage = Strings.Backup_Settings_PassphraseTooShort; return; }
            var passphrase = NewPassphrase;
            try { await Task.Run(() => keyStore.WriteKey(passphrase)).ConfigureAwait(true); }
            catch (SecretStoreUnavailableException)
            {
                // Senza passphrase salvata non si accende nulla: l'utente deve vedere che non e' andata (la causa resta fuori dall'interfaccia).
                TestResultMessage = Strings.Backup_Settings_KeyringUnavailable;
                return;
            }
            HasPassphrase = true;
        }
        await settings.SetAsync("backup.auto.enabled", IsEnabled ? "true" : "false");
        await settings.SetAsync("backup.auto.frequency_days", FrequencyDays.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await settings.SetAsync("backup.auto.retention_count", RetentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await settings.SetAsync("backup.auto.path", Folder);
        if (IsEnabled && HasPassphrase)
        {
            await settings.SetAsync(DisabledReasonKey, string.Empty);
            SelfDisabled = false;
        }
        NewPassphrase = string.Empty;
        ConfirmPassphrase = string.Empty;
        TestResultMessage = Strings.Backup_Settings_Saved;
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        TestResultMessage = null;
        // Lo scheduler legge la passphrase dal portachiavi, una chiamata sincrona: fuori dal thread dell'interfaccia.
        try { await Task.Run(() => runner.RunNowAsync()).ConfigureAwait(true); TestResultMessage = Strings.Backup_Settings_TestOk; }
        catch (SecretStoreUnavailableException) { TestResultMessage = Strings.Backup_Settings_KeyringUnavailable; }
        catch (Exception ex) { TestResultMessage = ex.Message; }
    }
}
