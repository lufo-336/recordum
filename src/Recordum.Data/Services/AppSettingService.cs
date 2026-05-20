using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public sealed partial class AppSettingService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ILogger<AppSettingService> logger) : IAppSettingService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "AppSetting {Key} set")]
    private partial void LogSet(string key);

    public async ValueTask<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        return row?.Value;
    }

    public async ValueTask SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = clock.UtcNow.UtcDateTime });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = clock.UtcNow.UtcDateTime;
        }
        await db.SaveChangesAsync(ct);
        LogSet(key);
    }

    public async ValueTask<T?> GetEnumAsync<T>(string key, CancellationToken ct = default) where T : struct, Enum
    {
        var raw = await GetAsync(key, ct);
        if (raw is null) return null;
        return Enum.TryParse<T>(raw, ignoreCase: true, out var parsed) ? parsed : null;
    }

    public ValueTask SetEnumAsync<T>(string key, T value, CancellationToken ct = default) where T : struct, Enum
        => SetAsync(key, value.ToString(), ct);
}
