namespace Recordum.WPF.Services;

public sealed class WpfBackupSaveFileDialog : IBackupSaveFileDialog
{
    public string? Prompt(string defaultFileName, string filter)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
