using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Recordum.Data.Sync;

/// <summary>Lo stato della sync legato a una connessione. Con <see cref="Applying"/> acceso (l'unione che scrive le righe
/// di un altro dispositivo) i trigger non lasciano lapidi né cambiano <c>updated_at_ms</c>: l'ora remota resta.</summary>
public sealed class SyncConnectionState
{
    private static readonly ConditionalWeakTable<DbConnection, SyncConnectionState> States = [];

    public bool Applying { get; set; }

    public static SyncConnectionState For(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return States.GetValue(connection, static _ => new SyncConnectionState());
    }
}
