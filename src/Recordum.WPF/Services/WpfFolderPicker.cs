using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class WpfFolderPicker : IFolderPicker
{
    public Task<string?> PickFolderAsync(string? initialFolder)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        if (!string.IsNullOrWhiteSpace(initialFolder)) dlg.InitialDirectory = initialFolder;
        return Task.FromResult(dlg.ShowDialog() == true ? dlg.FolderName : null);
    }
}
