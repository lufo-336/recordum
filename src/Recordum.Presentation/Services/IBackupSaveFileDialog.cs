namespace Recordum.Presentation.Services;

/// <summary>Abstracts the OS save-file dialog for backup file selection.</summary>
public interface IBackupSaveFileDialog
{
    /// <summary>Returns the chosen path, or null if the user cancelled.</summary>
    string? Prompt(string defaultFileName, string filter);
}
