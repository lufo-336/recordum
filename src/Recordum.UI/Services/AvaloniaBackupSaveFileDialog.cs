using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Recordum.Presentation.Services;

namespace Recordum.UI.Services;

/// <summary>Gemella di <c>WpfBackupSaveFileDialog</c> su <c>StorageProvider</c>.</summary>
public sealed class AvaloniaBackupSaveFileDialog(Func<Window?> ownerResolver) : IBackupSaveFileDialog
{
    public async Task<string?> PromptAsync(string defaultFileName, string filter)
    {
        if (ownerResolver() is not { } owner) return null;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = defaultFileName,
            DefaultExtension = "recbak",
            FileTypeChoices = AvaloniaFileDialogs.ParseFilter(filter),
        });
        return file is null ? null : await AvaloniaFileDialogs.LocalPathOrNoticeAsync(file, owner);
    }
}
