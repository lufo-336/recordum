namespace Recordum.Presentation.Services;

/// <summary>Il selettore di una cartella. Torna il percorso scelto, <c>null</c> se l'utente annulla.</summary>
public interface IFolderPicker
{
    Task<string?> PickFolderAsync(string? initialFolder);
}
