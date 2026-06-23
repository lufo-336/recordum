namespace Recordum.Data.Backup;

/// <summary>
/// Best-effort secure deletion: overwrites a file's bytes with zeros before unlinking, so
/// decrypted/plaintext artifacts (the temp DB dump and the decrypted archive produced during
/// restore) are not trivially recoverable from free space. Shared by <see cref="BackupService"/>
/// (temp staging directory) and <see cref="RestoreService"/> (plaintext DB dump + decrypted zip).
/// Not a hard guarantee on copy-on-write / wear-levelled storage, but matches the threat model
/// (casual forensic recovery of a just-deleted plaintext copy).
/// </summary>
internal static class SecureWipe
{
    public static void DeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            Overwrite(path);
            File.Delete(path);
        }
        catch { /* best-effort: never let cleanup failures surface */ }
    }

    public static void DeleteDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { Overwrite(f); } catch { /* best-effort per file */ }
            }
            Directory.Delete(dir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    private static void Overwrite(string path)
    {
        var fi = new FileInfo(path);
        if (fi.Length == 0) return;
        using var fs = File.Open(path, FileMode.Open, FileAccess.Write);
        var zero = new byte[(int)Math.Min(fi.Length, 65_536)];
        var remaining = fi.Length;
        while (remaining > 0)
        {
            var n = (int)Math.Min(zero.Length, remaining);
            fs.Write(zero, 0, n);
            remaining -= n;
        }
        fs.Flush(flushToDisk: true);
    }
}
