using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.UI.Bootstrap;

namespace Recordum.UI.Tests.Fixtures;

/// <summary>Un host con DB SQLCipher temporaneo PRIVATO e orologio finto (Ruling R8/R3): per i test che scrivono
/// dati o dipendono dal giorno. <c>configure</c> gira dopo AddRecordumCore, quindi puo' sostituire servizi.</summary>
public sealed class PrivateDbHost
{
    public FakeClock Clock { get; } = new();
    private readonly Lazy<ServiceProvider> _provider;
    public ServiceProvider Provider => _provider.Value;

    public PrivateDbHost(string prefix, Action<IServiceCollection>? configure = null) =>
        _provider = new(() => Build(prefix, configure).GetAwaiter().GetResult());

    private async Task<ServiceProvider> Build(string prefix, Action<IServiceCollection>? configure)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        var options = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dir], _ => null);
        var services = new ServiceCollection();
        services.AddRecordumCore(options);
        services.AddRecordumAvaloniaShell();
        services.AddSingleton<IDbKeyProvider>(new FixedKey());
        services.AddSingleton<IClock>(Clock);   // dopo AddRecordumCore: vince l'ultima registrazione
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<CoreInitializer>().InitializeAsync();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
#pragma warning disable CA1031 // best effort a fine processo
            try { SqliteConnection.ClearAllPools(); if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception) { }
#pragma warning restore CA1031
        };
        return provider;
    }

    public static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
        public long UtcNowMs => Now.ToUnixTimeMilliseconds();
        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FixedKey : IDbKeyProvider
    {
        private readonly byte[] _key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1)).ToArray();
        public string ProviderId => "fixed-test-key";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) => ValueTask.FromResult(new KeyMaterial([.. _key]));
    }
}
