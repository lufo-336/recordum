using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.Backup;

public sealed partial class BackupHistoryViewModel(IBackupLogService log) : ObservableObject
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
#pragma warning disable CA1822
    private void OpenFolder(BackupHistoryRowViewModel row)
#pragma warning restore CA1822
    {
        var folder = Path.GetDirectoryName(row.Path);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }
}
