using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.Startup;
using Recordum.UI.Bootstrap;
using Recordum.UI.Diagnostics;
using Recordum.UI.Tests.Fixtures;
using Xunit;

namespace Recordum.UI.Tests.Startup;

public class AppStartupTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), $"appstartup-{Guid.NewGuid():N}");

    private static RecordumOptions ResolveTempOptions(string dir) =>
        RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dir], _ => null);

    [AvaloniaFact]
    public async Task Steps_run_in_the_documented_order()
    {
        var dir = NewDir();
        CrashLogger.ResetForTests();
        var options = ResolveTempOptions(dir);
        var head = new FakeAppHead { Options = options };
        var host = new FakeStartupHost(options);
        var startup = new AppStartup(head, host);
        try
        {
            var sp = await startup.RunAsync();

            sp.Should().NotBeNull();
            startup.Timeline.Marks.Select(m => m.Step).Should().Equal(
                "options",
                "lock",
                "seed-refusal",
                "reconcile",
                "detect",
                "unlock",
                "container",
                "core-init",
                "culture-theme-rows",
                "home",
                "backup-scheduler",
                "sync-scheduler");
            head.Reported.Should().ContainSingle(line =>
                line.StartsWith("startup options=", StringComparison.Ordinal) && line.Contains("home=", StringComparison.Ordinal));
        }
        finally
        {
            await startup.DisposeAsync();
            SqliteConnection.ClearAllPools();
            CrashLogger.ResetForTests();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_scheduler_does_not_start_when_the_head_does_not_run_sync(bool runsSync)
    {
        var dir = NewDir();
        CrashLogger.ResetForTests();
        var options = ResolveTempOptions(dir);
        var head = new FakeAppHead { Options = options, RunsSync = runsSync };
        var host = new FakeStartupHost(options);
        var startup = new AppStartup(head, host);
        try
        {
            var sp = await startup.RunAsync();

            sp.Should().NotBeNull();
            var steps = startup.Timeline.Marks.Select(m => m.Step).ToList();
            if (runsSync) steps.Should().Contain("sync-scheduler");
            else steps.Should().NotContain("sync-scheduler");
        }
        finally
        {
            await startup.DisposeAsync();
            SqliteConnection.ClearAllPools();
            CrashLogger.ResetForTests();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Core_initialization_does_not_run_on_the_UI_thread()
    {
        var dir = NewDir();
        CrashLogger.ResetForTests();
        var options = ResolveTempOptions(dir);
        var head = new FakeAppHead { Options = options };
        var host = new FakeStartupHost(options);
        var step = new ThreadRecordingStep();
        var startup = new AppStartup(head, host, services => services.AddSingleton<IStartupStep>(step));
        try
        {
            var sp = await startup.RunAsync();

            sp.Should().NotBeNull();
            step.Called.Should().BeTrue();
            step.OnUiThread.Should().BeFalse();
        }
        finally
        {
            await startup.DisposeAsync();
            SqliteConnection.ClearAllPools();
            CrashLogger.ResetForTests();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task RunAsync_twice_returns_the_same_task_and_takes_the_lock_once()
    {
        var dir = NewDir();
        CrashLogger.ResetForTests();
        var options = ResolveTempOptions(dir);
        var head = new FakeAppHead { Options = options };
        var host = new FakeStartupHost(options);
        var startup = new AppStartup(head, host);
        try
        {
            var a = startup.RunAsync();
            var b = startup.RunAsync();
            b.Should().BeSameAs(a);

            var sp = await a;
            sp.Should().NotBeNull();
            startup.Timeline.Marks.Count(m => m.Step == "lock").Should().Be(1);
        }
        finally
        {
            await startup.DisposeAsync();
            SqliteConnection.ClearAllPools();
            CrashLogger.ResetForTests();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task A_failing_core_initialization_shows_the_fatal_page_with_the_log_path()
    {
        var dir = NewDir();
        CrashLogger.ResetForTests();
        var options = ResolveTempOptions(dir);
        var head = new FakeAppHead { Options = options };
        var host = new FakeStartupHost(options);
        var startup = new AppStartup(head, host, services => services.AddSingleton<IStartupStep>(new FailingStep()));
        try
        {
            var sp = await startup.RunAsync();

            sp.Should().BeNull();
            host.Calls.Should().Contain("fatal");
            host.Calls.Should().NotContain("main");
            host.FatalLogPath.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            await startup.DisposeAsync();
            SqliteConnection.ClearAllPools();
            CrashLogger.ResetForTests();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task A_second_instance_shows_already_running()
    {
        var dir = NewDir();
        CrashLogger.ResetForTests();
        var options = ResolveTempOptions(dir);
        var head = new FakeAppHead { Options = options };
        var host = new FakeStartupHost(options);
        var startup = new AppStartup(head, host);
        try
        {
            using var held = DataDirectoryLock.TryAcquire(dir);
            held.Should().NotBeNull();

            var sp = await startup.RunAsync();

            sp.Should().BeNull();
            host.Calls.Should().Contain("fatal");
            host.Calls.Should().NotContain("unlock");
            host.Calls.Should().NotContain("main");
            host.FatalMessage.Should().Be(Strings.Startup_AlreadyRunning);
        }
        finally
        {
            await startup.DisposeAsync();
            SqliteConnection.ClearAllPools();
            CrashLogger.ResetForTests();
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class FakeStartupHost(RecordumOptions options) : IStartupHost
    {
        public List<string> Calls { get; } = [];
        public string? FatalMessage { get; private set; }
        public string? FatalLogPath { get; private set; }

        public void ShowWaiting(string status) => Calls.Add($"waiting:{status}");

        public async Task<IDbKeyProvider?> UnlockAsync(UnlockViewModel viewModel)
        {
            Calls.Add("unlock");
            var unlock = AppComposition.CreateUnlockService(options, new NullSecretStore());
            var state = unlock.DetectState();
            var result = await unlock.UnlockAsync(state, "correct horse battery", RememberChoice.LeaveAsIs);
            return result.Provider;
        }

        public void ShowFatal(string message, string? logPath)
        {
            Calls.Add("fatal");
            FatalMessage = message;
            FatalLogPath = logPath;
        }

        public void ShowMain(IServiceProvider services) => Calls.Add("main");
    }

    private sealed class ThreadRecordingStep : IStartupStep
    {
        public bool Called { get; private set; }
        public bool? OnUiThread { get; private set; }

        public Task RunAsync(CancellationToken ct = default)
        {
            Called = true;
            OnUiThread = Dispatcher.UIThread.CheckAccess();
            return Task.CompletedTask;
        }
    }

    private sealed class FailingStep : IStartupStep
    {
        public Task RunAsync(CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }
}
