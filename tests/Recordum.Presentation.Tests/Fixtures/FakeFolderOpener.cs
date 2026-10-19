using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeFolderOpener : IFolderOpener
{
    public List<string> Opened { get; } = [];
    public Task OpenAsync(string folder) { Opened.Add(folder); return Task.CompletedTask; }
}
