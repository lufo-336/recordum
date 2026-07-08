using Microsoft.EntityFrameworkCore;

namespace Recordum.Data.Backup;

/// <summary>Reads the real schema_version AppSetting; the fallback covers pre-seed DBs only.</summary>
internal static class DbSchemaVersion
{
    public static async Task<string> ReadAsync(RecordumDbContext db, string fallback, CancellationToken ct)
    {
        var v = await db.AppSettings.AsNoTracking()
            .Where(a => a.Key == "schema_version").Select(a => a.Value)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
