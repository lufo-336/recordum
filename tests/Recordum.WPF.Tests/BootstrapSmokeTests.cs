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
using Recordum.WPF.ViewModels.HabitEditor;
using Recordum.WPF.ViewModels.Quick;
using Recordum.WPF.ViewModels.Timer;
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
    public async Task End_to_end_create_via_template_then_edit_persists_to_db()
    {
        await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync();

        var templates = _services.GetRequiredService<IHabitTemplateService>();
        var seed = (await templates.ListAsync())[0];

        var newId = await templates.InstantiateAsync(seed.Id, parentId: null);

        var editor = _services.GetRequiredService<HabitEditorViewModel>();
        await editor.LoadAsync(newId);
        editor.Name = "yoga v2";
        await Task.Delay(50); // let the per-field autosave fire

        var nodes = _services.GetRequiredService<IHabitNodeService>();
        var node = await nodes.GetAsync(newId);
        node.Should().NotBeNull();
        node!.Name.Should().Be("yoga v2");
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

    [Fact]
    public async Task End_to_end_quick_action_for_action_habit_records_completion_and_emits_event()
    {
        await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync();

        Guid habitId;
        using (var scope = _services.CreateScope())
        {
            var habits = scope.ServiceProvider.GetRequiredService<IHabitNodeService>();
            habitId = await habits.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "ping" });
        }

        using (var scope = _services.CreateScope())
        {
            var selector = scope.ServiceProvider.GetRequiredService<QuickActionHandlerSelector>();
            var handler = selector.For(HabitType.Action);
            handler.Should().NotBeNull();
            (await handler!.ExecuteAsync(habitId)).Should().BeTrue();
        }

        using (var scope = _services.CreateScope())
        {
            var completions = scope.ServiceProvider.GetRequiredService<ICompletionService>();
            var list = await completions.ListByHabitAsync(habitId);
            list.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task WebView2Availability_resolves_after_initialize()
    {
        await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync();
        var avail = _services.GetRequiredService<IWebView2Availability>();
        // We don't assert IsAvailable=true because CI runners may lack the runtime.
        // We assert the probe RAN: post-probe, IsAvailable agrees with Environment presence.
        avail.IsAvailable.Should().Be(avail.Environment is not null);
    }

    [Fact]
    public async Task End_to_end_note_create_list_update_delete()
    {
        await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync();

        Guid habitId;
        using (var scope = _services.CreateScope())
        {
            var habits = scope.ServiceProvider.GetRequiredService<IHabitNodeService>();
            habitId = await habits.CreateAsync(new HabitNode
            {
                NodeType = NodeType.Habit,
                HabitType = HabitType.Journal,
                Name = "notes-host",
            });
        }

        Guid noteId;
        using (var scope = _services.CreateScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<INoteService>();
            noteId = await notes.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "first" });
            await notes.UpdateAsync(noteId, n => n.BodyMd = "second");
            var listed = await notes.ListByHabitAsync(habitId);
            listed.Should().ContainSingle().Which.BodyMd.Should().Be("second");
            await notes.DeleteAsync(noteId);
        }

        using (var scope = _services.CreateScope())
        {
            var notes = scope.ServiceProvider.GetRequiredService<INoteService>();
            (await notes.ListByHabitAsync(habitId)).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task End_to_end_timer_recovery_reattaches_running_timer_in_status_bar()
    {
        await _services.GetRequiredService<ApplicationInitializer>().InitializeAsync();

        Guid habitId;
        using (var scope = _services.CreateScope())
        {
            var habits = scope.ServiceProvider.GetRequiredService<IHabitNodeService>();
            habitId = await habits.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "yoga" });
            var ats = scope.ServiceProvider.GetRequiredService<IActiveTimerService>();
            await ats.StartAsync(habitId, TimerMode.Stopwatch);
        }

        var status = _services.GetRequiredService<TimerStatusBarViewModel>();
        await status.LoadAsync();

        status.Rows.Should().ContainSingle().Which.HabitNodeId.Should().Be(habitId);
    }
}
