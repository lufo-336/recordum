namespace Recordum.Data.Models;

/// <summary>
/// Ruling 20: l'ora di un campo più vecchio della sua riga. Un campo senza riga qui ha l'ora della riga
/// (<c>updated_at_ms</c>). La tengono i trigger della sync e l'unione; non viaggia (viaggia come <c>_field_ms</c>).
/// </summary>
public sealed class SyncFieldMs
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public long Ms { get; set; }
}
