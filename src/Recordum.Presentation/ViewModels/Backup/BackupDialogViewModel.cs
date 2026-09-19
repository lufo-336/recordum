using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Backup;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Backup;

public enum PassphraseStrength { None, Weak, Ok, Strong }

public sealed partial class BackupDialogViewModel(IBackupService backup, IBackupSaveFileDialog saveDialog, IClock clock) : ObservableObject
{
    [ObservableProperty] private string _destination = string.Empty;
    [ObservableProperty] private string _passphrase = string.Empty;
    [ObservableProperty] private string _confirmPassphrase = string.Empty;
    [ObservableProperty] private string? _successPath;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isRunning;

    public PassphraseStrength Strength => Passphrase.Length switch
    {
        0 => PassphraseStrength.None,
        < 8 => PassphraseStrength.Weak,
        < 16 => PassphraseStrength.Ok,
        _ => PassphraseStrength.Strong,
    };

    partial void OnPassphraseChanged(string value) { OnPropertyChanged(nameof(Strength)); RunCommand.NotifyCanExecuteChanged(); }
    partial void OnConfirmPassphraseChanged(string value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnDestinationChanged(string value) => RunCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void Browse()
    {
        var path = saveDialog.Prompt($"recordum-{clock.UtcNow.UtcDateTime:yyyyMMdd-HHmm}.recbak", "Recordum backup (*.recbak)|*.recbak");
        if (!string.IsNullOrWhiteSpace(path)) Destination = path;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        IsRunning = true;
        ErrorMessage = null;
        try
        {
            var result = await backup.CreateAsync(Destination, Passphrase, new BackupOptions(BackupKind.Manual));
            SuccessPath = result.Path;
        }
        catch (BackupException ex) { ErrorMessage = ex.Code; }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsRunning = false; }
    }

    private bool CanRun() =>
        !IsRunning
        && !string.IsNullOrWhiteSpace(Destination)
        && Passphrase.Length >= 12
        && Passphrase == ConfirmPassphrase;
}
