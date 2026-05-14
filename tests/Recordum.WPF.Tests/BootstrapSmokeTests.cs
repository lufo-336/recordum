using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Data;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.Tests;

public sealed class BootstrapSmokeTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _services;

    public BootstrapSmokeTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"recordum-smoke-{Guid.NewGuid():N}.db");

        var collection = new ServiceCollection();
        collection.AddRecordumServices(new RecordumOptions { DatabasePath = _dbPath });
        _services = collection.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void ServiceProvider_resolves_every_registered_contract()
    {
        Assert.NotNull(_services.GetRequiredService<IClock>());
        Assert.NotNull(_services.GetRequiredService<IPluginContext>());
        Assert.NotNull(_services.GetRequiredService<IDbContextFactory<RecordumDbContext>>());
        Assert.NotNull(_services.GetRequiredService<IPermissionBroker>());
        Assert.NotNull(_services.GetRequiredService<IExtensionPointRegistry>());
        Assert.NotNull(_services.GetRequiredService<IEventBus>());
        Assert.NotNull(_services.GetRequiredService<IApiRouter>());
        Assert.NotNull(_services.GetRequiredService<IPluginHost>());
        Assert.NotNull(_services.GetRequiredService<IPluginManager>());
        Assert.NotNull(_services.GetRequiredService<ApplicationInitializer>());
    }

    [Fact]
    public async Task ApplicationInitializer_creates_database_and_applies_migrations()
    {
        var initializer = _services.GetRequiredService<ApplicationInitializer>();
        await initializer.InitializeAsync();

        Assert.True(File.Exists(_dbPath));

        var factory = _services.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        // Seed data shipped with InitialSchema migration must be present after init.
        Assert.True(await db.UnitsOfMeasure.AnyAsync(), "Seed data missing — migration did not run.");
        Assert.True(await db.ScaleTemplates.AnyAsync());
        Assert.True(await db.AppSettings.AnyAsync());
    }

    [Fact]
    public async Task ApplicationInitializer_is_idempotent_on_repeated_calls()
    {
        var initializer = _services.GetRequiredService<ApplicationInitializer>();
        await initializer.InitializeAsync();
        await initializer.InitializeAsync(); // No-op second time.

        var factory = _services.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var firstCount = await db.UnitsOfMeasure.CountAsync();
        Assert.True(firstCount > 0);
    }

    [Fact]
    public void PluginManager_starts_empty()
    {
        var manager = _services.GetRequiredService<IPluginManager>();
        Assert.Empty(manager.ListEnabled());
    }
}
