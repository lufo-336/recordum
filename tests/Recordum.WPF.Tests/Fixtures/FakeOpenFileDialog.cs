using Recordum.WPF.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeOpenFileDialog : IOpenFileDialog
{
    public string? Result { get; set; }
    public string? Prompt(string filter) => Result;
}
