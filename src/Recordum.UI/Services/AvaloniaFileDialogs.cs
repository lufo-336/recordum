using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Recordum.Presentation.Resources;
using Recordum.UI.Views.Startup;

namespace Recordum.UI.Services;

/// <summary>Il lavoro comune dei dialog di file su <c>TopLevel.StorageProvider</c>: il filtro e' nel formato WPF
/// che i ViewModel gia' passano ("Nome|*.a;*.b|Nome2|*.c"), un percorso non locale e' un avviso e non un'eccezione.</summary>
internal static class AvaloniaFileDialogs
{
    internal static IReadOnlyList<FilePickerFileType> ParseFilter(string filter)
    {
        var parts = filter.Split('|');
        var types = new List<FilePickerFileType>();
        for (var i = 0; i + 1 < parts.Length; i += 2)
            types.Add(new FilePickerFileType(parts[i]) { Patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) });
        return types;
    }

    /// <summary>Il percorso locale del file scelto; per uno non locale mostra l'avviso e torna null.</summary>
    internal static async Task<string?> LocalPathOrNoticeAsync(IStorageItem item, Window owner)
    {
        if (item.TryGetLocalPath() is { } path) return path;
        var notice = new ConfirmationWindow(Strings.Dialog_Ok, Strings.Dialog_NotLocalFile, okOnly: true);
        notice.YesButton.Click += (_, _) => notice.Close();
        await notice.ShowDialog(owner);
        return null;
    }
}
