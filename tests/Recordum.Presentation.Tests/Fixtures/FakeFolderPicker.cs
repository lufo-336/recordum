using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeFolderPicker : IFolderPicker
{
    public string? Result { get; set; }
    public string? AskedInitial { get; private set; }
    public Task<string?> PickFolderAsync(string? initialFolder) { AskedInitial = initialFolder; return Task.FromResult(Result); }
}
