using System.Diagnostics;
using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class WpfFolderOpener : IFolderOpener
{
    public Task OpenAsync(string folder)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
