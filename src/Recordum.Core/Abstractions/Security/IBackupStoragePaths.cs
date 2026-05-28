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
}
