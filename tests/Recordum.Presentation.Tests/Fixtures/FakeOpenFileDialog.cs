using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeOpenFileDialog : IOpenFileDialog
{
    public string? Result { get; set; }
    public Task<string?> PromptAsync(string filter) => Task.FromResult(Result);

    public string[] MultipleResult { get; set; } = [];
    public Task<string[]> PromptMultipleAsync(string filter) => Task.FromResult(MultipleResult);
}
