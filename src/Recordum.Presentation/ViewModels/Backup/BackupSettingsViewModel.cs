using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Backup;

public interface IBackupSchedulerRunner
{
    Task RunNowAsync(CancellationToken ct = default);
}

public sealed partial class BackupSettingsViewModel(
    IAppSettingService settings,
    IAutoBackupKeyStore keyStore,
    IBackupSchedulerRunner runner) : ObservableObject
{
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
        HasPassphrase = await Task.Run(keyStore.HasKey, ct).ConfigureAwait(true);
        SelfDisabled = !string.IsNullOrEmpty(await settings.GetAsync(DisabledReasonKey, ct).ConfigureAwait(true));
        OnPropertyChanged(nameof(InertWarning));
    }

    partial void OnIsEnabledChanged(bool value) => OnPropertyChanged(nameof(InertWarning));
    partial void OnNewPassphraseChanged(string value) => OnPropertyChanged(nameof(InertWarning));

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!string.IsNullOrEmpty(NewPassphrase))
        {
            if (NewPassphrase != ConfirmPassphrase) return;
            if (NewPassphrase.Length < 12) return;
            var passphrase = NewPassphrase;
            try { await Task.Run(() => keyStore.WriteKey(passphrase)).ConfigureAwait(true); }
            catch (SecretStoreUnavailableException ex)
            {
                // Senza passphrase salvata non si accende nulla: l'utente deve vedere che non e' andata.
                TestResultMessage = ex.Message;
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
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        TestResultMessage = null;
        try { await runner.RunNowAsync(); TestResultMessage = "ok"; }
        catch (Exception ex) { TestResultMessage = ex.Message; }
    }
}
