using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>La chiave dell'auto-backup passa da DPAPI (Windows-only, F6). "Non disponibile" per uno
/// store di chiavi è `HasKey() == false`, non un'eccezione: `BackupScheduler` la interroga prima di
/// agire e un false la ferma lì (esattamente come su Linux oggi, dove l'auto-backup è "innocuo ma
/// inutile" per lo stesso motivo, v. indagine di composizione §1).</summary>
public sealed class NullAutoBackupKeyStore : IAutoBackupKeyStore
{
    public bool HasKey() => false;

    public void WriteKey(string passphrase) { }

    public void DeleteKey() { }
}
