namespace Recordum.Core.Backup;

/// <param name="InactivePlugins">Gli id dei plugin del backup che qui non sono attivi. Solo id: la frase la compone la
/// presentazione, nella lingua dell'utente (F2c-6).</param>
public sealed record RestorePreview(BackupManifest Manifest, IReadOnlyList<string> InactivePlugins);
