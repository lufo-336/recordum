using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeBackupSaveFileDialog : IBackupSaveFileDialog
{
    public string? Result { get; set; }
    public string? LastSuggestedName { get; private set; }
    public string? LastFilter { get; private set; }
    public Task<string?> PromptAsync(string defaultFileName, string filter)
    {
        LastSuggestedName = defaultFileName;
        LastFilter = filter;
        return Task.FromResult(Result);
    }
}
