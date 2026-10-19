using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Recordum.Presentation.Services;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfFolderOpener</c> su <c>TopLevel.Launcher</c> (xdg-open su Linux, Explorer su Windows).
/// Senza finestra (headless, avvio) non fa niente.</summary>
public sealed class AvaloniaFolderOpener(Func<Window?> ownerResolver) : IFolderOpener
{
    public async Task OpenAsync(string folder)
    {
        if (ownerResolver() is not { } owner) return;
        await owner.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder));
    }
}
