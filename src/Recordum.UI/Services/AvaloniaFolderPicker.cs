using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Recordum.Presentation.Services;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfFolderPicker</c> su <c>StorageProvider</c>. Senza finestra (headless, avvio) risponde
/// come un annullamento; un percorso non locale e' un avviso, non un'eccezione.</summary>
public sealed class AvaloniaFolderPicker(Func<Window?> ownerResolver) : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string? initialFolder)
    {
        if (ownerResolver() is not { } owner) return null;
        var options = new FolderPickerOpenOptions { AllowMultiple = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            options.SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(initialFolder);
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count == 0 ? null : await AvaloniaFileDialogs.LocalPathOrNoticeAsync(folders[0], owner);
    }
}
