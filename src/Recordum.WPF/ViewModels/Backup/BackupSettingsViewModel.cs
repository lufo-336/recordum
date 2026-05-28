using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.Backup;

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

    public bool InertWarning => IsEnabled && !HasPassphrase && string.IsNullOrEmpty(NewPassphrase);

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsEnabled = (await settings.GetAsync("backup.auto.enabled", ct).ConfigureAwait(true)) == "true";
        if (int.TryParse(await settings.GetAsync("backup.auto.frequency_days", ct).ConfigureAwait(true), out var f)) FrequencyDays = f;
        if (int.TryParse(await settings.GetAsync("backup.auto.retention_count", ct).ConfigureAwait(true), out var r)) RetentionCount = r;
        Folder = await settings.GetAsync("backup.auto.path", ct).ConfigureAwait(true) ?? string.Empty;
        HasPassphrase = keyStore.HasKey();
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
            keyStore.WriteKey(NewPassphrase);
            HasPassphrase = true;
        }
        await settings.SetAsync("backup.auto.enabled", IsEnabled ? "true" : "false");
        await settings.SetAsync("backup.auto.frequency_days", FrequencyDays.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await settings.SetAsync("backup.auto.retention_count", RetentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await settings.SetAsync("backup.auto.path", Folder);
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
