namespace Recordum.Core.Backup;

public sealed record RestorePreview(BackupManifest Manifest, IReadOnlyList<string> Warnings);
