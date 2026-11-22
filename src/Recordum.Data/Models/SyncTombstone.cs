namespace Recordum.Data.Models;

/// <summary>
/// Lapide locale di un'entità eliminata (o assorbita da un'altra via <see cref="AliasOf"/>).
/// </summary>
public sealed class SyncTombstone
{
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public long DeletedAtMs { get; set; }
    public string? DeviceId { get; set; }
    public string? AliasOf { get; set; }
}
