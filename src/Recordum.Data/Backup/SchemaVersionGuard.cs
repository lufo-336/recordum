namespace Recordum.Data.Backup;

/// <summary>
/// Compares Recordum schema versions ("major.minor", e.g. "2.10") numerically per component, so
/// "2.10" is correctly newer than "2.7" (a naive string/float compare gets this wrong). Used by the
/// restore compatibility guard to reject a backup whose schema is newer than the running build can read.
/// </summary>
internal static class SchemaVersionGuard
{
    /// <summary>True if <paramref name="candidate"/> is a strictly newer schema than <paramref name="current"/>.</summary>
    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    internal static int Compare(string a, string b)
    {
        var (aMaj, aMin) = Parse(a);
        var (bMaj, bMin) = Parse(b);
        return aMaj != bMaj ? aMaj.CompareTo(bMaj) : aMin.CompareTo(bMin);
    }

    private static (int Major, int Minor) Parse(string version)
    {
        var parts = (version ?? string.Empty).Split('.');
        _ = int.TryParse(parts.ElementAtOrDefault(0), out var major);
        _ = int.TryParse(parts.ElementAtOrDefault(1), out var minor);
        return (major, minor);
    }
}
