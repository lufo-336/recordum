namespace Recordum.Presentation.Services;

/// <summary>Apre una cartella nel file manager del sistema.</summary>
public interface IFolderOpener
{
    Task OpenAsync(string folder);
}
