namespace Recordum.Data.Models;

/// <summary>
/// Ultimo stato condiviso noto per coppia (dispositivo pari, entità, chiave) in forma JSON pubblica.
/// </summary>
public sealed class SyncBase
{
    public string PeerDeviceId { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string RowJson { get; set; } = string.Empty;
}
