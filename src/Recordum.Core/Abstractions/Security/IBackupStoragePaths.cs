namespace Recordum.Core.Abstractions.Security;

/// <summary>
/// Exposes backup-specific storage paths in addition to the DB paths.
/// Implemented by <c>RecordumOptions</c> in the WPF host.
/// </summary>
public interface IBackupStoragePaths : IDbStoragePaths
{
    string AttachmentsDirectory { get; }
    string BackupsDirectory { get; }
    string AutoBackupKeyPath { get; }

    /// <summary>Dove backup e restore appoggiano i file in chiaro mentre lavorano: dentro la cartella dati, mai la
    /// temp di sistema — su Linux <c>/tmp</c> è leggibile da ogni utente locale (RV-4 della revisione 2026-09-25).</summary>
    string TempDirectory { get; }
}
