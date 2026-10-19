using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.ViewModels.Backup;

public sealed partial class BackupHistoryViewModel(IBackupLogService log, IFolderOpener folderOpener) : ObservableObject
{
    public ObservableCollection<BackupHistoryRowViewModel> Rows { get; } = [];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Rows.Clear();
        foreach (var r in await log.ListAsync(50, ct).ConfigureAwait(true))
            Rows.Add(new BackupHistoryRowViewModel(r));
    }

    [RelayCommand]
    private async Task DeleteAsync(BackupHistoryRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        await log.DeleteAsync(row.Id).ConfigureAwait(true);
        try { if (File.Exists(row.Path)) File.Delete(row.Path); } catch { /* best-effort */ }
        Rows.Remove(row);
    }

    [RelayCommand]
    private async Task OpenFolderAsync(BackupHistoryRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var folder = Path.GetDirectoryName(row.Path);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        await folderOpener.OpenAsync(folder).ConfigureAwait(true);
    }
}
