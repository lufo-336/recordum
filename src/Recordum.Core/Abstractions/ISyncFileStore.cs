namespace Recordum.Core.Abstractions;

/// <summary>Dove stanno i file di scambio: una cartella sui computer, documenti scelti col selettore sul telefono.</summary>
public interface ISyncFileStore
{
    Task<IReadOnlyList<SyncPeerFile>> ListPeerFilesAsync(CancellationToken ct);

    Task<Stream> OpenReadAsync(SyncPeerFile file, CancellationToken ct);

    /// <summary>Scrive il proprio file in modo atomico: un errore a metà lascia intatto il file di prima.</summary>
    Task WriteOwnAsync(Func<Stream, Task> write, CancellationToken ct);
}

public sealed record SyncPeerFile(string Id, string DisplayName, DateTimeOffset? LastModified);
