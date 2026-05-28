namespace Recordum.WPF.Services;

public sealed class WpfOpenFileDialog : IOpenFileDialog
{
    public string? Prompt(string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
