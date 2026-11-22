namespace Recordum.Data.Sync;

/// <summary>Come viene trattato un campo durante la serializzazione e l'unione.</summary>
public enum SyncFieldKind
{
    Plain,
    /// <summary>Testo libero lungo soggetto alla regola B (due versioni in coda).</summary>
    Text,
    /// <summary>BLOB binario esternalizzato in photos/.</summary>
    Photo,
}

public sealed record SyncField(
    string Column,
    string PublicName,
    SyncFieldKind Kind);

public sealed record SyncEntity(
    string Table,
    string PublicName,
    IReadOnlyList<string> KeyColumns,
    IReadOnlyList<SyncField> Fields,
    IReadOnlyList<string> NaturalKey,
    bool IsHistory);
