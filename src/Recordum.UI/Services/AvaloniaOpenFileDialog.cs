using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Recordum.Presentation.Services;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfOpenFileDialog</c> su <c>StorageProvider</c>. Senza finestra principale (headless,
/// avvio) risponde come un annullamento.</summary>
public sealed class AvaloniaOpenFileDialog(Func<Window?> ownerResolver) : IOpenFileDialog
{
    public async Task<string?> PromptAsync(string filter) => (await PickAsync(filter, false)).FirstOrDefault();

    public async Task<string[]> PromptMultipleAsync(string filter) => [.. await PickAsync(filter, true)];

    private async Task<List<string>> PickAsync(string filter, bool multiple)
    {
        var paths = new List<string>();
        if (ownerResolver() is not { } owner) return paths;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = multiple,
            FileTypeFilter = AvaloniaFileDialogs.ParseFilter(filter),
        });
        foreach (var f in files)
            if (await AvaloniaFileDialogs.LocalPathOrNoticeAsync(f, owner) is { } p) paths.Add(p);
        return paths;
    }
}
