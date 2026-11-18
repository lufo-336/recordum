using Microsoft.Data.Sqlite;
using Recordum.Core.Text;

namespace Recordum.Data;

/// <summary>Funzioni C# esposte a SQLite per le migrazioni che le servono in SQL puro (ScaleAnchorsV220).
/// Microsoft.Data.Sqlite le tiene sulla connessione attraverso chiusure e riaperture.</summary>
public static class SqliteFunctions
{
    public static void Register(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.CreateFunction("recordum_tag_slug", (string? label) => TagNames.FromLabel(label), isDeterministic: true);
        // Il formato in cui EF Core scrive un Guid in SQLite: TEXT "D" maiuscolo.
        connection.CreateFunction("recordum_new_guid", () => Guid.NewGuid().ToString("D").ToUpperInvariant());
    }
}
