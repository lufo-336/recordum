using Recordum.WPF.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeBackupSaveFileDialog : IBackupSaveFileDialog
{
    public string? Result { get; set; }
    public string? Prompt(string defaultFileName, string filter) => Result;
}
