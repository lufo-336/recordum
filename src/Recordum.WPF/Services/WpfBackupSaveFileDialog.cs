using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class WpfBackupSaveFileDialog : IBackupSaveFileDialog
{
    public Task<string?> PromptAsync(string defaultFileName, string filter) => Task.FromResult(Prompt(defaultFileName, filter));

    private static string? Prompt(string defaultFileName, string filter)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
