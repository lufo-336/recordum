namespace Recordum.Data.Security;

/// <summary>F1-SEC1: su Unix il database cifrato e i suoi <c>-wal</c>/<c>-shm</c> sono 0600. Il file nasce
/// 0600 (<c>UnixCreateMode</c>, nessuna finestra con l'umask 022) e SQLite crea <c>-wal</c>/<c>-shm</c>
/// con gli stessi permessi del database (misurato in <c>DatabaseFilePermissionsTests</c>). Windows: niente.</summary>
internal static class DatabaseFilePermissions
{
    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Crea il database vuoto 0600 se manca (SQLite apre un file da 0 byte come nuovo).</summary>
    public static void CreatePrivate(string databasePath)
    {
        if (OperatingSystem.IsWindows() || File.Exists(databasePath)) return;
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        try
        {
            using var _ = new FileStream(databasePath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.ReadWrite, UnixCreateMode = Private,
            });
        }
        catch (IOException) when (File.Exists(databasePath)) { /* nato nel frattempo: lo riporta a 0600 Realign */ }
    }

    /// <summary>Riporta a 0600 i tre file preesistenti più aperti. Non tocca owner né contenuto.</summary>
    public static void Realign(string databasePath)
    {
        if (OperatingSystem.IsWindows()) return;
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
            if (File.Exists(path) && File.GetUnixFileMode(path) != Private) File.SetUnixFileMode(path, Private);
    }
}
