using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;

namespace Recordum.Data.Sync;

/// <summary>
/// Legge <c>sync_meta.device_name</c> alla prima richiesta e lo tiene in memoria.
/// Riceve <see cref="IServiceProvider"/> invece di <see cref="IDbContextFactory{RecordumDbContext}"/>
/// per evitare il ciclo DI con <c>TimestampInterceptor</c> nelle opzioni della factory.
/// </summary>
public sealed class DeviceIdentity(IServiceProvider serviceProvider) : IDeviceIdentity
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly Lock _gate = new();
    private bool _loaded;
    private string? _deviceName;
    private int _generation;

    public string? DeviceName
    {
        get
        {
            int generation;
            lock (_gate)
            {
                if (_loaded) return _deviceName;
                generation = _generation;
            }

            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
            using var db = factory.CreateDbContext();
            var name = db.SyncMeta.AsNoTracking()
                .Select(m => m.DeviceName)
                .FirstOrDefault();

            lock (_gate)
            {
                if (!_loaded && _generation == generation)
                {
                    _deviceName = name;
                    _loaded = true;
                }
                return _loaded ? _deviceName : name;
            }
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _loaded = false;
            _deviceName = null;
        }
    }
}
