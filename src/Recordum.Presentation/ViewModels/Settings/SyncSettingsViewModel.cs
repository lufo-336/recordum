using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Settings;

/// <summary>Impostazioni › Unisci: unione tra dispositivi tramite file <c>.rcsync</c> nella cartella cloud.</summary>
public sealed partial class SyncSettingsViewModel(
    IAppSettingService settings,
    IFolderPicker folderPicker,
    IOpenFileDialog openDialog,
    IBackupSaveFileDialog saveDialog,
    IConfirmationDialog confirm,
    IClock clock,
    ISyncService sync,
    SyncSchedulerHost scheduler) : ObservableObject
{
    public const string AutoMergeKey = SyncSchedulerHost.EnabledSettingKey;
    public const string FolderKey = "sync.folder";
    public const string IncludePhotosKey = "sync.include_photos";

    private bool _loading;
    private bool _isMerging;
    private bool _isPlainBusy;

    [ObservableProperty] private bool _autoMerge = true;
    [ObservableProperty] private bool _includePhotos;
    [ObservableProperty] private string? _folder;
    [ObservableProperty] private string _newPassphrase = string.Empty;
    [ObservableProperty] private string _confirmPassphrase = string.Empty;
    [ObservableProperty] private string _plainPassphrase = string.Empty;
    [ObservableProperty] private string? _passphraseMessage;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsJoinChoice), nameof(IsJoined))]
    private SyncJoinState _joinState = SyncJoinState.NotConfigured;

    public bool NeedsJoinChoice => JoinState == SyncJoinState.NeedsChoice;
    public bool IsJoined => JoinState == SyncJoinState.Joined;

    public ObservableCollection<string> Status { get; } = [];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _loading = true;
        try
        {
            var autoRaw = await settings.GetAsync(AutoMergeKey, ct).ConfigureAwait(true);
            AutoMerge = !string.Equals(autoRaw, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(autoRaw, "0", StringComparison.Ordinal);

            var photosRaw = await settings.GetAsync(IncludePhotosKey, ct).ConfigureAwait(true);
            // Acceso di partenza (spec §9): spento solo se l'utente l'ha spento.
            IncludePhotos = !string.Equals(photosRaw, "false", StringComparison.OrdinalIgnoreCase);

            Folder = await settings.GetAsync(FolderKey, ct).ConfigureAwait(true);
        }
        finally
        {
            _loading = false;
        }

        await RefreshSyncStateAsync(ct).ConfigureAwait(true);
    }

    partial void OnAutoMergeChanged(bool value)
    {
        if (_loading) return;
        _ = PersistAsync(AutoMergeKey, value, nudge: value);
    }

    private async Task PersistAsync(string key, bool value, bool nudge = false)
    {
        try
        {
            await settings.SetAsync(key, value ? "true" : "false").ConfigureAwait(true);
            if (nudge) scheduler.Nudge();
        }
#pragma warning disable CA1031 // fire-and-forget: l'errore si mostra nella pagina invece di perdersi
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
    }

    partial void OnIncludePhotosChanged(bool value)
    {
        if (_loading) return;
        _ = PersistAsync(IncludePhotosKey, value);
    }

    partial void OnPlainPassphraseChanged(string value) => NotifyPlainCommandsCanExecuteChanged();

    [RelayCommand]
    private async Task ChooseFolderAsync(CancellationToken ct)
    {
        var picked = await folderPicker.PickFolderAsync(Folder).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(picked)) return;
        Folder = picked;
        await settings.SetAsync(FolderKey, picked, ct).ConfigureAwait(true);
        await RefreshSyncStateAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMergeNow))]
    private async Task MergeNowAsync(CancellationToken ct)
    {
        if (_isMerging) return;   // ExecuteAsync non consulta CanExecute
        _isMerging = true;
        MergeNowCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;
        try
        {
            // CancellationToken.None: un secondo invoke cancella il token del comando e libererebbe la guardia mentre il giro e' in corso
            await sync.RunAsync(CancellationToken.None).ConfigureAwait(true);
            await RefreshSyncStateAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            _isMerging = false;
            MergeNowCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanMergeNow() => !_isMerging;

    [RelayCommand]
    private async Task SetPassphraseAsync(CancellationToken ct)
    {
        PassphraseMessage = null;
        if (NewPassphrase != ConfirmPassphrase)
        {
            PassphraseMessage = Strings.Backup_Settings_PassphraseMismatch;
            return;
        }
        if (NewPassphrase.Length < 12)
        {
            PassphraseMessage = Strings.Backup_Settings_PassphraseTooShort;
            return;
        }
        try
        {
            await sync.SetPassphraseAsync(NewPassphrase, ct).ConfigureAwait(true);
            NewPassphrase = string.Empty;
            ConfirmPassphrase = string.Empty;
            PassphraseMessage = Strings.Backup_Settings_Saved;
            await RefreshSyncStateAsync(ct).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { PassphraseMessage = ex.Message; }
#pragma warning restore CA1031
    }

    [RelayCommand(CanExecute = nameof(CanRunPlain))]
    private async Task ExportPlainAsync(CancellationToken ct)
    {
        if (_isPlainBusy || string.IsNullOrEmpty(PlainPassphrase)) return;
        var inPath = await openDialog.PromptAsync("Recordum Sync (*.rcsync)|*.rcsync").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(inPath)) return;
        var outPath = await saveDialog.PromptAsync("recordum-sync.zip", "ZIP (*.zip)|*.zip").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(outPath)) return;

        _isPlainBusy = true;
        NotifyPlainCommandsCanExecuteChanged();
        ErrorMessage = null;
        try
        {
            await using var rcsync = File.OpenRead(inPath);
            await using var zip = File.Create(outPath);
            await sync.ExportPlainAsync(rcsync, zip, PlainPassphrase, ct).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            PlainPassphrase = string.Empty;   // la frase non resta in memoria ne' nel campo dopo l'uso
            _isPlainBusy = false;
            NotifyPlainCommandsCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunPlain))]
    private async Task ReencryptAsync(CancellationToken ct)
    {
        if (_isPlainBusy || string.IsNullOrEmpty(PlainPassphrase)) return;
        var inPath = await openDialog.PromptAsync("ZIP (*.zip)|*.zip").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(inPath)) return;
        var outPath = await saveDialog.PromptAsync("recordum-sync.rcsync", "Recordum Sync (*.rcsync)|*.rcsync").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(outPath)) return;

        _isPlainBusy = true;
        NotifyPlainCommandsCanExecuteChanged();
        ErrorMessage = null;
        try
        {
            await using var zip = File.OpenRead(inPath);
            await using var rcsync = File.Create(outPath);
            await sync.ReencryptAsync(zip, rcsync, PlainPassphrase, ct).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            PlainPassphrase = string.Empty;   // la frase non resta in memoria ne' nel campo dopo l'uso
            _isPlainBusy = false;
            NotifyPlainCommandsCanExecuteChanged();
        }
    }

    private bool CanRunPlain() => !_isPlainBusy && !string.IsNullOrEmpty(PlainPassphrase);

    private void NotifyPlainCommandsCanExecuteChanged()
    {
        ExportPlainCommand.NotifyCanExecuteChanged();
        ReencryptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task DisconnectAsync(CancellationToken ct)
    {
        if (!await confirm.ConfirmAsync(Strings.Sync_Disconnect_Label, Strings.Sync_Disconnect_Hint).ConfigureAwait(true))
            return;
        ErrorMessage = null;
        try
        {
            await sync.DisconnectAsync(ct).ConfigureAwait(true);
            await RefreshSyncStateAsync(ct).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
    }

    [RelayCommand]
    private async Task JoinStartFromOthersAsync(CancellationToken ct)
    {
        if (!await confirm.ConfirmAsync(Strings.Sync_Join_Confirm_Title, Strings.Sync_Join_Confirm_Message).ConfigureAwait(true))
            return;
        ErrorMessage = null;
        try
        {
            await sync.JoinAsync(SyncJoinChoice.StartFromOthers, ct).ConfigureAwait(true);
            await RefreshSyncStateAsync(ct).ConfigureAwait(true);
        }
        catch (SyncJoinRefusedException) { ErrorMessage = Strings.Sync_Join_NoPeerFile; }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
    }

    [RelayCommand]
    private async Task JoinKeepBothAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        try
        {
            await sync.JoinAsync(SyncJoinChoice.KeepThisDeviceToo, ct).ConfigureAwait(true);
            await RefreshSyncStateAsync(ct).ConfigureAwait(true);
        }
        catch (SyncJoinRefusedException) { ErrorMessage = Strings.Sync_Join_NoPeerFile; }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
    }

    private async Task RefreshSyncStateAsync(CancellationToken ct)
    {
        JoinState = await sync.GetJoinStateAsync(ct).ConfigureAwait(true);
        var status = await sync.GetStatusAsync(ct).ConfigureAwait(true);
        PopulateStatus(status);
    }

    private void PopulateStatus(SyncStatus status)
    {
        var culture = CultureInfo.CurrentCulture;
        Status.Clear();
#pragma warning disable CA1863 // la cultura può cambiare dopo il caricamento della classe (es. nei test)
        Status.Add(status.LastRun is { } lastRun
            ? string.Format(culture, Strings.Sync_Status_LastRun, FormatLastRun(lastRun, clock, culture))
            : Strings.Sync_Status_Never);

        foreach (var peer in status.Peers)
            Status.Add(FormatPeer(peer, clock, culture));

        if (status.TextConflicts > 0)
            Status.Add(string.Format(culture, Strings.Sync_Status_Conflicts, status.TextConflicts));

        if (status.OwnFileBytes > 0 || status.LastRun is not null)
            Status.Add(string.Format(culture, Strings.Sync_Status_FileSize, FormatBytes(status.OwnFileBytes, culture)));
#pragma warning restore CA1863
    }

    private static string FormatLastRun(DateTimeOffset instant, IClock clock, CultureInfo culture)
    {
        var local = TimeZoneInfo.ConvertTime(instant, clock.LocalTimeZone);
        var today = TimeZoneInfo.ConvertTime(clock.UtcNow, clock.LocalTimeZone).Date;
        return local.Date == today
            ? $"{Strings.Group_Section_Today.ToLower(culture)} {local.ToString("t", culture)}"
            : local.ToString("g", culture);
    }

#pragma warning disable CA1863 // la cultura può cambiare nei test
    private static string FormatPeer(SyncPeerStatus peer, IClock clock, CultureInfo culture)
    {
        if (peer.Problem is { } problem)
        {
            return problem switch
            {
                SyncPeerProblem.CannotOpen => string.Format(culture, Strings.Sync_Status_Problem_CannotOpen, peer.DeviceName),
                SyncPeerProblem.NewerFormat => string.Format(culture, Strings.Sync_Status_Problem_NewerFormat, peer.DeviceName),
                SyncPeerProblem.BrokenFile => string.Format(culture, Strings.Sync_Status_Problem_BrokenFile, peer.DeviceName),
                SyncPeerProblem.SameIdentity => Strings.Sync_Status_Problem_SameIdentity,
                _ => string.Format(culture, Strings.Sync_Status_Problem_BrokenFile, peer.DeviceName),
            };
        }

        if (peer.FileWrittenAt is { } writtenAt)
        {
            var local = TimeZoneInfo.ConvertTime(writtenAt, clock.LocalTimeZone);
            var today = TimeZoneInfo.ConvertTime(clock.UtcNow, clock.LocalTimeZone).Date;
            var formatted = local.Date == today ? local.ToString("t", culture) : local.ToString("g", culture);
            return string.Format(culture, Strings.Sync_Status_Peer, peer.DeviceName, formatted);
        }

        return string.Format(culture, Strings.Sync_Status_Peer, peer.DeviceName, Strings.Sync_Status_Never);
    }

    private static string FormatBytes(long bytes, CultureInfo culture) => bytes switch
    {
        < 1024 => string.Format(culture, "{0} B", bytes),
        < 1024 * 1024 => string.Format(culture, "{0:F1} KB", bytes / 1024.0),
        _ => string.Format(culture, "{0:F1} MB", bytes / 1048576.0),
    };
#pragma warning restore CA1863
}
