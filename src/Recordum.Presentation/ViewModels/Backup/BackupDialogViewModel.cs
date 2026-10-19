using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Backup;
using Recordum.Presentation.Resources;
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

    public string StrengthLabel => Strength switch
    {
        PassphraseStrength.Weak => Strings.Backup_Strength_Weak,
        PassphraseStrength.Ok => Strings.Backup_Strength_Ok,
        PassphraseStrength.Strong => Strings.Backup_Strength_Strong,
        _ => string.Empty,
    };

    partial void OnPassphraseChanged(string value) { OnPropertyChanged(nameof(Strength)); OnPropertyChanged(nameof(StrengthLabel)); RunCommand.NotifyCanExecuteChanged(); }
    partial void OnConfirmPassphraseChanged(string value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnDestinationChanged(string value) => RunCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = await saveDialog.PromptAsync($"recordum-{clock.UtcNow.UtcDateTime:yyyyMMdd-HHmm}.recbak", Strings.Backup_FileFilter);
        if (!string.IsNullOrWhiteSpace(path)) Destination = path;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (IsRunning) return;   // ExecuteAsync non consulta CanExecute: un secondo clic non lancia un secondo backup
        IsRunning = true;
        RunCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;
        try
        {
            var result = await backup.CreateAsync(Destination, Passphrase, new BackupOptions(BackupKind.Manual));
            SuccessPath = result.Path;
        }
#pragma warning disable CA1863 // CompositeFormat caching not worth it for a once-per-dialog UI string
        catch (BackupException ex)
        {
            ErrorMessage = ex.Code == "db_busy_retry_later" ? Strings.Backup_Errors_DbBusy
                : string.Format(CultureInfo.CurrentCulture, Strings.Backup_Errors_Failed, ex.Code);
        }
        catch (Exception ex) { ErrorMessage = string.Format(CultureInfo.CurrentCulture, Strings.Backup_Errors_Failed, ex.Message); }
#pragma warning restore CA1863
        finally { IsRunning = false; RunCommand.NotifyCanExecuteChanged(); }
    }

    private bool CanRun() =>
        !IsRunning
        && !string.IsNullOrWhiteSpace(Destination)
        && Passphrase.Length >= 12
        && Passphrase == ConfirmPassphrase;
}
