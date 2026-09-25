namespace Recordum.Core.Abstractions.Security;

/// <summary>Dove vive la passphrase dell'auto-backup. Su Windows la custodisce DPAPI (<c>DpapiAutoBackupKeyStore</c>
/// in Data); su Linux la decisione D3 della revisione 2026-09-25 (portachiavi di sistema o file 0600) è ancora aperta,
/// e fino ad allora l'auto-backup lì resta spento — e lo dice. Vive in Core perché anche <c>BackupScheduler</c>, in
/// Data, la chiede qui invece di leggere il file con DPAPI da sé (Passo 0, task 0.7).</summary>
public interface IAutoBackupKeyStore
{
    bool HasKey();

    void WriteKey(string passphrase);

    void DeleteKey();

    /// <summary>La passphrase, o <c>null</c> se manca o non si riesce a leggere.</summary>
    string? ReadKey();
}
