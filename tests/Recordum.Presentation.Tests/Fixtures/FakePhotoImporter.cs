using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakePhotoImporter : IPhotoImporter
{
    public static readonly byte[] FakeContent = [1, 2, 3];
    public static readonly byte[] FakeThumbnail = [9];

    public List<string> ImportCalls { get; } = [];

    /// <summary>Paths (case-insensitive) that fail to import, to simulate one bad file in a batch.</summary>
    public Dictionary<string, Exception> ThrowOnImport { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<ImportedPhoto> ImportAsync(string sourcePath, CancellationToken ct)
    {
        ImportCalls.Add(sourcePath);
        if (ThrowOnImport.TryGetValue(sourcePath, out var ex)) throw ex;
        return Task.FromResult(new ImportedPhoto(Guid.NewGuid(), sourcePath.Split('\\', '/')[^1], "image/jpeg", FakeContent, FakeThumbnail));
    }

    public byte[]? TryMakeThumbnail(byte[] content) => FakeThumbnail;
}
