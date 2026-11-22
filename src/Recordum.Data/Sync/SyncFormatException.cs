namespace Recordum.Data.Sync;

/// <summary>
/// Eccezione sollevata quando il contenuto interno di un archivio di scambio <c>recordum-sync</c>
/// viola il contratto del formato (manifest mancante o incompatibile, versione maggiore diversa, JSON corrotto).
/// </summary>
public sealed class SyncFormatException(
    string reason,
    Exception? innerException = null)
    : Exception(reason, innerException)
{
    public string Reason { get; } = reason;
}
