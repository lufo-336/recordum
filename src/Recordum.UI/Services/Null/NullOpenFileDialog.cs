using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>Il file dialog d'apertura (foto della nota, restore) resta con `StorageProvider` al di là
/// di F1 (F2/F4/F6). L'interfaccia dichiara <see cref="PromptMultiple"/> "mai null" per l'utente
/// che annulla: qui vale lo stesso, un array vuoto — non una lista con dentro null.</summary>
public sealed class NullOpenFileDialog : IOpenFileDialog
{
    public string? Prompt(string filter) => null;

    public string[] PromptMultiple(string filter) => [];
}
