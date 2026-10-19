using Recordum.Core.Backup;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeRestoreService : IRestoreService
{
    public IReadOnlyList<string> Unavailable { get; set; } = [];
    public Exception? RestoreThrows { get; set; }

    public Task<RestorePreview> PreviewAsync(string sourcePath, string passphrase, CancellationToken ct = default)
        => Task.FromResult(new RestorePreview(
            new BackupManifest("2.7", "3.10.0", DateTime.UtcNow, BackupKind.Manual, 1,
                new BackupCounts(1, 2, 0, 0),
                Array.Empty<PluginManifestSnapshot>(),
                new PermissionState(new Dictionary<string, IReadOnlyList<string>>())),
            Array.Empty<string>()));

    public Task<RestoreResult> RestoreAsync(string sourcePath, string passphrase, RestoreOptions options, CancellationToken ct = default)
        => RestoreThrows is { } ex ? Task.FromException<RestoreResult>(ex) : Task.FromResult(new RestoreResult(Unavailable));
}
