using Recordum.Core.Abstractions;

namespace Recordum.Data.Sync;

/// <summary>
/// I file di scambio in una cartella (computer): i <c>*.rcsync</c> degli altri, e il proprio scritto su
/// <c>&lt;proprio&gt;.tmp</c> nella stessa cartella e poi rinominato: un errore a metà lascia intatto il file di prima.
/// </summary>
public sealed class FolderSyncFileStore(string folder, string ownFileName) : ISyncFileStore
{
    public Task<IReadOnlyList<SyncPeerFile>> ListPeerFilesAsync(CancellationToken ct)
    {
        IReadOnlyList<SyncPeerFile> peers = !Directory.Exists(folder)
            ? []
            : new DirectoryInfo(folder).EnumerateFiles("*.rcsync")
                .Where(f => !string.Equals(f.Name, ownFileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => new SyncPeerFile(f.Name, f.Name, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList();
        return Task.FromResult(peers);
    }

    public Task<Stream> OpenReadAsync(SyncPeerFile file, CancellationToken ct) =>
        Task.FromResult<Stream>(new FileStream(
            Path.Combine(folder, Path.GetFileName(file.Id)), FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

    public async Task WriteOwnAsync(Func<Stream, Task> write, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var own = Path.Combine(folder, ownFileName);
        var tmp = own + ".tmp";
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await write(fs).ConfigureAwait(false);
                fs.Flush(flushToDisk: true);   // i byte sul disco prima della rinomina, anche se manca la corrente
            }

            File.Move(tmp, own, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { }   // l'errore che conta è quello della scrittura
            throw;
        }
    }
}
