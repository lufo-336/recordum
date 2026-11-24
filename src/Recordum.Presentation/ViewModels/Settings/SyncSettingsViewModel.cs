using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Data.Backup;
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
    SyncSchedulerHost scheduler,
    IBackupStoragePaths paths,
    IHabitNavigator habitNavigator) : ObservableObject
{
    public const string AutoMergeKey = SyncSchedulerHost.EnabledSettingKey;
    public const string FolderKey = "sync.folder";
    public const string IncludePhotosKey = "sync.include_photos";

    private bool _loading;
    private bool _isMerging;
    private bool _isPlainBusy;
    private bool _isJoining;

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
    public ObservableCollection<SyncItemToFixViewModel> ItemsToFix { get; } = [];

    [ObservableProperty] private bool _isItemsListOpen;

    public bool HasItemsToFix => ItemsToFix.Count > 0;

#pragma warning disable CA1863 // la cultura può cambiare dopo il caricamento della classe (es. nei test)
    public string ItemsToFixSummary => ItemsToFix.Count switch
    {
        0 => string.Empty,
        1 => Strings.Sync_Items_One,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.Sync_Items_Many, ItemsToFix.Count),
    };
#pragma warning restore CA1863

    public event EventHandler? CloseRequested;

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
        _ = PersistAsync(IncludePhotosKey, value, nudge: AutoMerge);
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
        if (AutoMerge) scheduler.Nudge();
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

    [RelayCommand(CanExecute = nameof(CanModifyConnection))]
    private async Task SetPassphraseAsync(CancellationToken ct)
    {
        if (_isJoining) return;   // ExecuteAsync non consulta CanExecute
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
        _isJoining = true;
        NotifyConnectionCommandsCanExecuteChanged();
        try
        {
            // CancellationToken.None: un secondo invoke non deve annullare l'operazione già in corso
            await sync.SetPassphraseAsync(NewPassphrase, CancellationToken.None).ConfigureAwait(true);
            NewPassphrase = string.Empty;
            ConfirmPassphrase = string.Empty;
            PassphraseMessage = Strings.Backup_Settings_Saved;
            await RefreshSyncStateAsync(CancellationToken.None).ConfigureAwait(true);
            if (AutoMerge) scheduler.Nudge();
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { PassphraseMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            _isJoining = false;
            NotifyConnectionCommandsCanExecuteChanged();
        }
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
        string? tempPath = null;
        try
        {
            // I byte in chiaro passano solo da TempDirectory: la destinazione non si tronca prima di sapere che la frase va.
            var tempDir = paths.TempDirectory;
            Directory.CreateDirectory(tempDir);
            tempPath = Path.Combine(tempDir, $"recordum-plain-{Guid.NewGuid():N}.zip");
            await using (var rcsync = File.OpenRead(inPath))
            await using (var zip = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await sync.ExportPlainAsync(rcsync, zip, PlainPassphrase, ct).ConfigureAwait(true);
            File.Move(tempPath, outPath, overwrite: true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            if (tempPath is not null) SecureWipe.DeleteFile(tempPath);
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
        string? tempPath = null;
        try
        {
            // Temporaneo cifrato accanto alla destinazione (rinomina atomica sullo stesso volume, nome .tmp fuori da *.rcsync).
            var fullOut = Path.GetFullPath(outPath);
            var outDir = Path.GetDirectoryName(fullOut)!;
            Directory.CreateDirectory(outDir);
            tempPath = Path.Combine(outDir, $".{Path.GetFileName(fullOut)}.{Guid.NewGuid():N}.tmp");
            await using (var zip = File.OpenRead(inPath))
            await using (var rcsync = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await sync.ReencryptAsync(zip, rcsync, PlainPassphrase, ct).ConfigureAwait(true);
            File.Move(tempPath, outPath, overwrite: true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            if (tempPath is not null)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
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

    private bool CanModifyConnection() => !_isJoining;

    private void NotifyConnectionCommandsCanExecuteChanged()
    {
        JoinKeepBothCommand.NotifyCanExecuteChanged();
        JoinStartFromOthersCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        SetPassphraseCommand.NotifyCanExecuteChanged();
    }

    private static string JoinRefusalMessage(SyncJoinRefusal reason) => reason switch
    {
        SyncJoinRefusal.MissingConfiguration => Strings.Sync_Join_MissingConfiguration,
        SyncJoinRefusal.NoReadablePeer => Strings.Sync_Join_NoPeerFile,
        _ => Strings.Sync_Join_NoPeerFile,
    };

    [RelayCommand(CanExecute = nameof(CanModifyConnection))]
    private async Task DisconnectAsync(CancellationToken ct)
    {
        if (_isJoining) return;   // ExecuteAsync non consulta CanExecute
        _isJoining = true;
        NotifyConnectionCommandsCanExecuteChanged();
        try
        {
            if (!await confirm.ConfirmAsync(Strings.Sync_Disconnect_Label, Strings.Sync_Disconnect_Hint).ConfigureAwait(true))
                return;
            ErrorMessage = null;
            await sync.DisconnectAsync(CancellationToken.None).ConfigureAwait(true);
            Folder = null;
            await RefreshSyncStateAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            _isJoining = false;
            NotifyConnectionCommandsCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyConnection))]
    private async Task JoinStartFromOthersAsync(CancellationToken ct)
    {
        if (_isJoining) return;   // ExecuteAsync non consulta CanExecute
        _isJoining = true;
        NotifyConnectionCommandsCanExecuteChanged();
        try
        {
            if (!await confirm.ConfirmAsync(Strings.Sync_Join_Confirm_Title, Strings.Sync_Join_Confirm_Message).ConfigureAwait(true))
                return;
            ErrorMessage = null;
            await sync.JoinAsync(SyncJoinChoice.StartFromOthers, CancellationToken.None).ConfigureAwait(true);
            await RefreshSyncStateAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (SyncJoinRefusedException ex) { ErrorMessage = JoinRefusalMessage(ex.Reason); }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            _isJoining = false;
            NotifyConnectionCommandsCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyConnection))]
    private async Task JoinKeepBothAsync(CancellationToken ct)
    {
        if (_isJoining) return;   // ExecuteAsync non consulta CanExecute
        _isJoining = true;
        NotifyConnectionCommandsCanExecuteChanged();
        ErrorMessage = null;
        try
        {
            await sync.JoinAsync(SyncJoinChoice.KeepThisDeviceToo, CancellationToken.None).ConfigureAwait(true);
            await RefreshSyncStateAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (SyncJoinRefusedException ex) { ErrorMessage = JoinRefusalMessage(ex.Reason); }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
        finally
        {
            _isJoining = false;
            NotifyConnectionCommandsCanExecuteChanged();
        }
    }

    private async Task RefreshSyncStateAsync(CancellationToken ct)
    {
        JoinState = await sync.GetJoinStateAsync(ct).ConfigureAwait(true);
        var status = await sync.GetStatusAsync(ct).ConfigureAwait(true);
        var items = await sync.ListItemsToFixAsync(ct).ConfigureAwait(true);
        PopulateStatus(status);
        PopulateItemsToFix(items);
    }

    [RelayCommand]
    private void ToggleItemsList()
    {
        if (!HasItemsToFix)
        {
            IsItemsListOpen = false;
            return;
        }
        IsItemsListOpen = !IsItemsListOpen;
    }

    [RelayCommand]
    private async Task OpenItemAsync(SyncItemToFixViewModel? item)
    {
        if (item is null) return;
        CloseRequested?.Invoke(this, EventArgs.Empty);
        await habitNavigator.OpenHabitAtDayAsync(item.HabitNodeId, item.HabitName, item.Day).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AcceptRepairAsync(SyncItemToFixViewModel? item)
    {
        if (item is null || !item.CanAccept) return;
        ErrorMessage = null;
        try
        {
            await sync.AcceptRepairAsync(item.EntityId, CancellationToken.None).ConfigureAwait(true);
            await RefreshSyncStateAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // errore mostrato nella pagina senza far cadere la finestra
        catch (Exception ex) { ErrorMessage = ex.Message; }
#pragma warning restore CA1031
    }

    private void PopulateItemsToFix(IReadOnlyList<SyncItemToFix> items)
    {
        ItemsToFix.Clear();
        foreach (var item in items)
            ItemsToFix.Add(new SyncItemToFixViewModel(item));
        if (ItemsToFix.Count == 0)
            IsItemsListOpen = false;
        OnPropertyChanged(nameof(HasItemsToFix));
        OnPropertyChanged(nameof(ItemsToFixSummary));
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
