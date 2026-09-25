namespace Recordum.Data.Security;

/// <summary>Una sola istanza di Recordum per cartella dati (RV-3 della revisione 2026-09-25). Due istanze sullo
/// stesso database sono la corsa al primo avvio che riscriveva il salt, e le scritture concorrenti che danno
/// <c>SQLITE_BUSY</c> (B-dbLock) — anche fra WPF e Avalonia, che su Windows condividono la cartella.
/// <para>Su Unix <see cref="FileShare.None"/> è un lock <i>advisory</i> (<c>flock</c>): vale fra processi .NET,
/// non contro un <c>rm</c>. È quanto serve qui: tenere fuori una seconda istanza, non proteggere i file.</para></summary>
public sealed class DataDirectoryLock : IDisposable
{
    public const string FileName = "recordum.lock";

    private readonly FileStream _stream;

    private DataDirectoryLock(FileStream stream) => _stream = stream;

    /// <summary>Il lock, da tenere per tutta la vita del processo, oppure <c>null</c> se un'altra istanza tiene già
    /// questa cartella.</summary>
    public static DataDirectoryLock? TryAcquire(string dataDirectory)
    {
        // Prima la cartella: senza, FileStream lancerebbe DirectoryNotFoundException — un'IOException che qui
        // sotto verrebbe scambiata per "un'altra istanza è aperta".
        Directory.CreateDirectory(dataDirectory);
        try
        {
            return new DataDirectoryLock(new FileStream(
                Path.Combine(dataDirectory, FileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();
}
