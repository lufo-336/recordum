using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.ViewModels;
using FluentAssertions;

namespace Recordum.WPF.Tests;

public sealed class BootstrapSmokeTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _services;

    private readonly string _keyPath;
    private readonly string _saltPath;

    public BootstrapSmokeTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"recordum-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "recordum.db");
        _keyPath = Path.Combine(dir, "db.key");
        _saltPath = Path.Combine(dir, "db.salt");

        var collection = new ServiceCollection();
        collection.AddRecordumServices(new RecordumOptions
        {
            DatabasePath = _dbPath,
            KeyFilePath = _keyPath,
            SaltFilePath = _saltPath,
        });
        _services = collection.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        var dir = Path.GetDirectoryName(_dbPath)!;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* file lock — best-effort cleanup */ }
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

    [Fact]
    public async Task End_to_end_bootstrap_creates_encrypted_db_and_serves_home_tree()
    {
        var initializer = _services.GetRequiredService<ApplicationInitializer>();
        await initializer.InitializeAsync();

        Assert.True(File.Exists(_dbPath));
        Assert.True(File.Exists(_keyPath));

        using var scope = _services.CreateScope();
        var habits = scope.ServiceProvider.GetRequiredService<IHabitNodeService>();
        var query = scope.ServiceProvider.GetRequiredService<IHabitTreeQueryService>();
        await habits.CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit,
            HabitType = HabitType.Action,
            Name = "ping",
        });
        Assert.Single(await query.GetHomeTreeAsync());

        // Plaintext open against the encrypted file must fail on first read.
        SqliteConnection.ClearAllPools();
        await using var plain = new SqliteConnection($"Data Source={_dbPath}");
        await plain.OpenAsync();
        using var cmd = plain.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master";
        await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteReaderAsync());
    }

    [Fact]
    public async Task End_to_end_bootstrap_resolves_main_window_view_model_and_loads_tree()
    {
        await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync();

        var vm = _services.GetRequiredService<MainWindowViewModel>();
        await vm.InitializeAsync();

        vm.Home.State.Should().BeOneOf(HomeState.Empty, HomeState.Loaded);
        vm.Home.Rows.Should().BeEmpty();
        vm.Settings.Should().NotBeNull();
    }
}
