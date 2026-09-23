using Recordum.Core.Enums;
using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>Il file dialog dell'export resta con `StorageProvider` al di là di F1 (F6). "Annullato"
/// per un save-file dialog è null: nessun percorso scelto. <see cref="WriteAsync"/> non è
/// raggiungibile finché Prompt torna sempre null — nessun chiamante ha un percorso da scrivere — ma
/// non lancia: un Null onora l'interfaccia, non la aggira.</summary>
public sealed class NullSaveFileDialog : ISaveFileDialog
{
    public string? Prompt(string suggestedFileName, ExportFormat format) => null;

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct = default) => Task.CompletedTask;
}
