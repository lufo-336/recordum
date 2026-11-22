using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recordum.Core.Abstractions;
using Recordum.Core.Models;

namespace Recordum.Data.Interceptors;

public sealed class TimestampInterceptor(
    IClock clock,
    IPluginContext pluginContext,
    IDeviceIdentity deviceIdentity) : SaveChangesInterceptor
{
    private static readonly HashSet<string> ProvenanceTypes =
        ["HabitNode", "HabitTemplate", "ExportTemplate", "Chart"];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? db)
    {
        if (db is null) return;

        var nowDt = clock.UtcNow.UtcDateTime;
        var nowMs = clock.UtcNowMs;
        var pluginId = pluginContext.PluginId;
        var deviceNameLoaded = false;
        string? deviceName = null;

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var typeName = entry.Entity.GetType().Name;

            if (entry.State == EntityState.Added)
            {
                TrySet(entry, "CreatedAt", nowDt);
                if (pluginId is not null && ProvenanceTypes.Contains(typeName) && Get(entry, "CreatedByPluginId") is null)
                {
                    TrySet(entry, "CreatedByPluginId", pluginId);
                }
            }

            TrySet(entry, "UpdatedAt", nowDt);
            TrySet(entry, "UpdatedAtMs", nowMs);

            if (ShouldStampDeviceName(entry))
            {
                // Pigro: IDeviceIdentity.DeviceName può aprire il DB alla prima lettura.
                if (!deviceNameLoaded)
                {
                    deviceName = deviceIdentity.DeviceName;
                    deviceNameLoaded = true;
                }
                TrySet(entry, "LastEditedDeviceName", deviceName);
            }
        }
    }

    private static bool ShouldStampDeviceName(EntityEntry entry)
    {
        var textPropertyName = entry.Entity switch
        {
            Note => nameof(Note.BodyMd),
            Completion => nameof(Completion.Notes),
            _ => null,
        };
        if (textPropertyName is null) return false;

        var prop = entry.Property(textPropertyName);
        if (entry.State == EntityState.Added)
        {
            return prop.CurrentValue is string text && !string.IsNullOrEmpty(text);
        }

        return prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue);
    }

    private static void TrySet(EntityEntry entry, string propertyName, object? value)
    {
        var prop = entry.Metadata.FindProperty(propertyName);
        if (prop is null) return;
        entry.Property(propertyName).CurrentValue = value;
    }

    private static object? Get(EntityEntry entry, string propertyName)
    {
        var prop = entry.Metadata.FindProperty(propertyName);
        return prop is null ? null : entry.Property(propertyName).CurrentValue;
    }
}
