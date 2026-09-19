using Recordum.Core.Abstractions.Security;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Plugins.Contracts.Manifest;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

// Shared fakes for tests that need to construct DiagnosticInfoViewModel
// (promoted from DiagnosticInfoViewModelTests' private nested fakes).

public sealed class FakeBackupStoragePaths : IBackupStoragePaths
{
    public string DatabasePath => "C:/fake/recordum.db";
    public string KeyFilePath => "C:/fake/db.key";
    public string SaltFilePath => "C:/fake/db.salt";
    public string AttachmentsDirectory => "C:/fake/attachments";
    public string BackupsDirectory => "C:/fake/backups";
    public string AutoBackupKeyPath => "C:/fake/autobackup.key";
}

public sealed class FakePluginManagerDiag : IPluginManager
{
    public List<PluginManifest> Manifests { get; } = [];
    public Task EnableAsync(PluginManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
    public Task DisableAsync(string pluginId, CancellationToken ct = default) => Task.CompletedTask;
    public IReadOnlyCollection<string> ListEnabled() => [];
    public IReadOnlyList<PluginManifest> ListActivatedManifests() => Manifests;
}

public sealed class FakeClipboardDiag : IClipboard
{
    public string? LastText { get; private set; }
    public void SetText(string text) => LastText = text;
}
