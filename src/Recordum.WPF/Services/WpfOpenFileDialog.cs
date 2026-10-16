using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class WpfOpenFileDialog : IOpenFileDialog
{
    public Task<string?> PromptAsync(string filter) => Task.FromResult(Prompt(filter));

    private static string? Prompt(string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public Task<string[]> PromptMultipleAsync(string filter) => Task.FromResult(PromptMultiple(filter));

    private static string[] PromptMultiple(string filter)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, CheckFileExists = true, Multiselect = true };
        return dlg.ShowDialog() == true ? dlg.FileNames : [];
    }
}
