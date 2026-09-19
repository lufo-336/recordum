using Recordum.Core.Enums;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeSaveFileDialog : ISaveFileDialog
{
    public string? Result { get; set; }
    public string? LastSavedPath { get; private set; }
    public byte[]? LastBytes { get; private set; }

    public string? Prompt(string suggestedFileName, ExportFormat format) => Result;

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct = default)
    {
        LastSavedPath = path;
        LastBytes = bytes;
        return Task.CompletedTask;
    }
}
