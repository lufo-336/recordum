using Recordum.Core.Abstractions.Security;

namespace Recordum.UI.Services.Null;

/// <summary>Finché la shell Avalonia non ha un key store vero (DPAPI su Windows, decisione D3 su Linux: Passo 0,
/// task 0.7) la chiave dell'auto-backup non c'è: <c>ReadKey()</c> restituisce null, e <c>BackupScheduler</c> spegne
/// l'auto-backup scrivendolo nel log. <c>WriteKey</c> <b>non</b> finge di riuscire: un'impostazione che l'utente crede
/// salvata e che non lo è sarebbe la trappola peggiore per un backup.</summary>
public sealed class NullAutoBackupKeyStore : IAutoBackupKeyStore
{
    public bool HasKey() => false;

    public void WriteKey(string passphrase) =>
        throw new NotSupportedException("The auto-backup passphrase cannot be stored yet on this platform.");

    public void DeleteKey() { }

    public string? ReadKey() => null;
}
