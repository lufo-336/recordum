namespace Recordum.Presentation.ViewModels.Backup;

public interface IBackupDialogLauncher
{
    Task ShowBackupAsync();
    /// <summary>True se il restore ha sostituito il database: l'app si sta chiudendo e il database resta chiuso fino
    /// all'uscita (F2c-2), quindi chi chiama non lo deve piu' interrogare.</summary>
    Task<bool> ShowRestoreAsync();
}
