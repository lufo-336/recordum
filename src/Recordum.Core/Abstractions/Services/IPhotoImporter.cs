using Recordum.Core.Models;

namespace Recordum.Core.Abstractions.Services;

public interface IPhotoImporter
{
    /// <summary>Reads the file (read-only, nothing written to disk) and normalizes it per the
    /// <c>photos.import_mode</c> setting. Throws <see cref="Errors.PhotoImportException"/>.</summary>
    Task<ImportedPhoto> ImportAsync(string sourcePath, CancellationToken ct);

    /// <summary>Clean thumbnail (long side 320) of already-stored bytes, or null if they cannot be decoded.</summary>
    byte[]? TryMakeThumbnail(byte[] content);
}
