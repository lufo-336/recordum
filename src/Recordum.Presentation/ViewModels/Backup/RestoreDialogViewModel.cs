using System.Globalization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Backup;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Backup;

// ponytail: sync optional until ISyncService is registered (Task 9)
public sealed partial class RestoreDialogViewModel(IRestoreService restore, IOpenFileDialog openDialog, ISyncService? sync = null) : ObservableObject
{
    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private string _passphrase = string.Empty;
    [ObservableProperty] private BackupManifest? _manifest;
    [ObservableProperty] private bool _isPreviewLoaded;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private string? _errorMessage;
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<string> UnavailablePlugins { get; } = [];

    partial void OnSourcePathChanged(string value) { ResetPreview(); }
    partial void OnPassphraseChanged(string value) { ResetPreview(); }

    private void ResetPreview()
    {
        IsPreviewLoaded = false;
        Manifest = null;
        Warnings.Clear();
        UnavailablePlugins.Clear();
        IsCompleted = false;
        PreviewCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var p = await openDialog.PromptAsync(Strings.Backup_FileFilter);
        if (!string.IsNullOrWhiteSpace(p)) SourcePath = p;
    }

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task PreviewAsync()
    {
        ErrorMessage = null;
        try
        {
            var p = await restore.PreviewAsync(SourcePath, Passphrase);
            Manifest = p.Manifest;
            Warnings.Clear();
#pragma warning disable CA1863 // CompositeFormat caching not worth it for a once-per-dialog UI string
            foreach (var id in p.InactivePlugins)
                Warnings.Add(string.Format(CultureInfo.CurrentCulture, Strings.Restore_PluginInactive, id));
#pragma warning restore CA1863
            if (sync is not null && await sync.GetJoinStateAsync(CancellationToken.None) == SyncJoinState.Joined)
                Warnings.Add(Strings.Restore_SyncActiveNote);
            IsPreviewLoaded = true;
            RunCommand.NotifyCanExecuteChanged();
        }
        catch (RestoreException ex) { ErrorMessage = Readable(ex.Code); }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (IsRunning) return;   // ExecuteAsync non consulta CanExecute
        IsRunning = true;
        RunCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;
        try
        {
            var r = await restore.RestoreAsync(SourcePath, Passphrase, new RestoreOptions(SkipSafetySnapshot: false));
            UnavailablePlugins.Clear();
            foreach (var p in r.UnavailablePlugins) UnavailablePlugins.Add(p);
            IsCompleted = true;
        }
        catch (RestoreException ex) { ErrorMessage = Readable(ex.Code); }
        finally { IsRunning = false; RunCommand.NotifyCanExecuteChanged(); }
    }

    private static string Readable(string code) => code switch
    {
        "invalid_format" => Strings.Restore_Errors_InvalidFormat,
        "invalid_passphrase" => Strings.Restore_Errors_InvalidPassphrase,
        "incompatible_schema" => Strings.Restore_Errors_IncompatibleSchema,
        "safety_snapshot_failed" => Strings.Restore_Errors_SafetySnapshotFailed,
        "busy" => Strings.Restore_Errors_Busy,
        "swap_failed" => Strings.Restore_Errors_SwapFailed,
#pragma warning disable CA1863 // CompositeFormat caching not worth it for a once-per-dialog UI string
        _ => string.Format(CultureInfo.CurrentCulture, Strings.Restore_Errors_Failed, code),
#pragma warning restore CA1863
    };

    private bool CanPreview() =>
        !string.IsNullOrWhiteSpace(SourcePath) && Passphrase.Length >= 12 && !IsRunning;

    private bool CanRun() => IsPreviewLoaded && !IsRunning && !IsCompleted;
}
