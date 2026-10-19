using Recordum.Core.Backup;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeBackupService : IBackupService
{
    public List<(string Path, string Pass, BackupKind Kind)> Calls { get; } = [];
    public Exception? Throws { get; set; }

    public Task<BackupResult> CreateAsync(string destinationPath, string passphrase, BackupOptions options, CancellationToken ct = default)
    {
        Calls.Add((destinationPath, passphrase, options.Kind));
        if (Throws is { } ex) return Task.FromException<BackupResult>(ex);
        return Task.FromResult(new BackupResult(
            Guid.NewGuid(), destinationPath, 1,
            new BackupManifest("2.7", "3.10.0", DateTime.UtcNow, options.Kind, 1,
                new BackupCounts(0, 0, 0, 0),
                Array.Empty<PluginManifestSnapshot>(),
                new PermissionState(new Dictionary<string, IReadOnlyList<string>>()))));
    }
}
