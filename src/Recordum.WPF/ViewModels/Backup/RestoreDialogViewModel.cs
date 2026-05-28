using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Backup;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.Backup;

public sealed partial class RestoreDialogViewModel(IRestoreService restore, IOpenFileDialog openDialog) : ObservableObject
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
    private void Browse()
    {
        var p = openDialog.Prompt("Recordum backup (*.recbak)|*.recbak");
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
            foreach (var w in p.Warnings) Warnings.Add(w);
            IsPreviewLoaded = true;
            RunCommand.NotifyCanExecuteChanged();
        }
        catch (RestoreException ex) { ErrorMessage = ex.Code; }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        IsRunning = true;
        try
        {
            var r = await restore.RestoreAsync(SourcePath, Passphrase, new RestoreOptions(SkipSafetySnapshot: false));
            UnavailablePlugins.Clear();
            foreach (var p in r.UnavailablePlugins) UnavailablePlugins.Add(p);
            IsCompleted = true;
        }
        catch (RestoreException ex) { ErrorMessage = ex.Code; }
        finally { IsRunning = false; }
    }

    private bool CanPreview() =>
        !string.IsNullOrWhiteSpace(SourcePath) && Passphrase.Length >= 12 && !IsRunning;

    private bool CanRun() => IsPreviewLoaded && !IsRunning && !IsCompleted;
}
