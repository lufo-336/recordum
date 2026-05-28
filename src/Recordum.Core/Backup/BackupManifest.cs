namespace Recordum.Core.Backup;

public sealed record BackupManifest(
    string SchemaVersion,
    string AppVersion,
    DateTime CreatedAtUtc,
    BackupKind Kind,
    long SizeBytesUncompressed,
    BackupCounts Counts,
    IReadOnlyList<PluginManifestSnapshot> Plugins,
    PermissionState Permissions);
