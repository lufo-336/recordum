using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Theming;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.DiagnosticInfo;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.DiagnosticInfo;

public class DiagnosticInfoViewModelTests
{
    // ---- helpers ----

    private static DiagnosticInfoViewModel BuildVm(
        FakeAppSettingService? settings = null,
        FakeThemeService? theme = null,
        FakeWebView2Availability? webView2 = null,
        FakeBackupStoragePaths? storagePaths = null,
        FakeBackupLogService? backupLog = null,
        FakePluginManagerDiag? pluginManager = null,
        FakeClipboardDiag? clipboard = null)
    {
        settings ??= new FakeAppSettingService();
        theme ??= new FakeThemeService();
        webView2 ??= new FakeWebView2Availability(available: false);
        storagePaths ??= new FakeBackupStoragePaths();
        backupLog ??= new FakeBackupLogService();
        pluginManager ??= new FakePluginManagerDiag();
        clipboard ??= new FakeClipboardDiag();
        return new DiagnosticInfoViewModel(settings, theme, webView2, storagePaths, backupLog, pluginManager, clipboard);
    }

    // ---- tests ----

    [Fact]
    public async Task LoadAsync_populates_rows_with_expected_labels()
    {
        var vm = BuildVm();
        await vm.LoadAsync();

        var labels = vm.Rows.Select(r => r.Label).ToList();
        labels.Should().Contain("App version");
        labels.Should().Contain("Schema version");
        labels.Should().Contain("Theme");
        labels.Should().Contain("WebView2 runtime installed");
        labels.Should().Contain("DB path");
        labels.Should().Contain("Last backup");
        labels.Should().Contain("Plugins activated");
        labels.Should().Contain("Git branch");
    }

    [Fact]
    public async Task LoadAsync_with_no_backups_shows_nobackups_message()
    {
        var backupLog = new FakeBackupLogService(); // empty by default
        var vm = BuildVm(backupLog: backupLog);
        await vm.LoadAsync();

        var row = vm.Rows.Single(r => r.Label == "Last backup");
        row.Value.Should().Be(Strings.Diagnostic_NoBackups);
    }

    [Fact]
    public async Task LoadAsync_with_a_backup_shows_created_at_and_kind()
    {
        var backupLog = new FakeBackupLogService();
        backupLog.Rows.Add(new BackupLog
        {
            Id = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 5, 28, 10, 30, 0, DateTimeKind.Utc),
            Kind = "Manual",
        });
        var vm = BuildVm(backupLog: backupLog);
        await vm.LoadAsync();

        var row = vm.Rows.Single(r => r.Label == "Last backup");
        row.Value.Should().Contain("2026-05-28");
        row.Value.Should().Contain("Manual");
    }

    [Fact]
    public async Task LoadAsync_lists_activated_plugins_by_id_and_version()
    {
        var plugins = new FakePluginManagerDiag();
        plugins.Manifests.Add(BuildManifest("io.recordum.chart-engine", "3.10.0"));
        plugins.Manifests.Add(BuildManifest("io.recordum.export-engine", "3.10.0"));

        var vm = BuildVm(pluginManager: plugins);
        await vm.LoadAsync();

        var row = vm.Rows.Single(r => r.Label == "Plugins activated");
        row.Value.Should().Contain("io.recordum.chart-engine 3.10.0");
        row.Value.Should().Contain("io.recordum.export-engine 3.10.0");
    }

    [Fact]
    public async Task CopyToClipboardCommand_sets_CopyConfirmed_then_resets()
    {
        var clipboard = new FakeClipboardDiag();
        var vm = BuildVm(clipboard: clipboard);
        await vm.LoadAsync();

        await vm.CopyToClipboardCommand.ExecuteAsync(null);

        // CopyConfirmed should have been true while awaiting; after the full await it should be false.
        // We verify the clipboard was called and that CopyConfirmed ended false.
        clipboard.LastText.Should().NotBeNullOrEmpty();
        vm.CopyConfirmed.Should().BeFalse(); // reset after 2s delay
    }

    [Fact]
    public async Task CopyToClipboard_text_contains_all_labels()
    {
        var clipboard = new FakeClipboardDiag();
        var vm = BuildVm(clipboard: clipboard);
        await vm.LoadAsync();

        await vm.CopyToClipboardCommand.ExecuteAsync(null);

        clipboard.LastText.Should().Contain("App version:");
        clipboard.LastText.Should().Contain("Schema version:");
        clipboard.LastText.Should().Contain("Last backup:");
    }

    // ---- helpers ----

    private static PluginManifest BuildManifest(string id, string version) => new()
    {
        SchemaVersion = "1",
        Id = id,
        Name = id,
        Version = version,
        Publisher = new ManifestPublisher { Id = "recordum", DisplayName = "Recordum" },
        MinRecordumVersion = "3.10.0",
        Entry = "entry.dll",
    };

    // Fakes promoted to Fixtures/FakeDiagnosticDependencies.cs (shared with settings-window tests).
}
