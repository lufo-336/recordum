using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions;

namespace Recordum.Data.Sync;

/// <summary>
/// Legge <c>sync_meta.device_name</c> alla prima richiesta e lo tiene in memoria.
/// Riceve <see cref="IServiceProvider"/> invece di <see cref="IDbContextFactory{RecordumDbContext}"/>
/// per evitare il ciclo DI con <c>TimestampInterceptor</c> nelle opzioni della factory.
/// <para>La lettura passa da una seconda connessione dentro <c>SavingChanges</c>: dietro una transazione grande che
/// tiene il DB, aspetterebbe il busy_timeout e farebbe fallire il salvataggio. Per questo <c>CoreInitializer</c>
/// riempie la cache all'avvio, e un fallimento qui torna null (registrato, non in cache): manca solo il nome sul
/// testo, il salvataggio resta.</para>
/// </summary>
public sealed partial class DeviceIdentity(IServiceProvider serviceProvider, ILogger<DeviceIdentity>? logger = null)
    : IDeviceIdentity
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger _logger = logger ?? NullLogger<DeviceIdentity>.Instance;
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

            string? name;
            try
            {
                var factory = _serviceProvider.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
                using var db = factory.CreateDbContext();
                name = db.SyncMeta.AsNoTracking()
                    .Select(m => m.DeviceName)
                    .FirstOrDefault();
            }
#pragma warning disable CA1031 // qualunque errore: il nome è un'etichetta, il salvataggio dell'utente viene prima
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogReadFailed(_logger, ex);
                return null;
            }

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

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Device name unavailable: reading sync_meta failed")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);
}
