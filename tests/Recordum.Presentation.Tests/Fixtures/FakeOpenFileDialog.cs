using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeOpenFileDialog : IOpenFileDialog
{
    public string? Result { get; set; }
    public string? LastFilter { get; private set; }
    public Task<string?> PromptAsync(string filter) { LastFilter = filter; return Task.FromResult(Result); }

    public string[] MultipleResult { get; set; } = [];
    /// <summary>Se impostato, la scelta resta in sospeso finche' il test non la completa.</summary>
    public Task<string[]>? PendingMultiple { get; set; }
    public Task<string[]> PromptMultipleAsync(string filter) => PendingMultiple ?? Task.FromResult(MultipleResult);
}
