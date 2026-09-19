using Recordum.Presentation.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeBackupSaveFileDialog : IBackupSaveFileDialog
{
    public string? Result { get; set; }
    public string? LastSuggestedName { get; private set; }
    public string? Prompt(string defaultFileName, string filter)
    {
        LastSuggestedName = defaultFileName;
        return Result;
    }
}
