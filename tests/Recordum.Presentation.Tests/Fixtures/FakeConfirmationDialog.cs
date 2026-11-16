using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeConfirmationDialog : IConfirmationDialog
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }
    public string? LastTitle { get; private set; }
    public string? LastMessage { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Calls++;
        LastTitle = title;
        LastMessage = message;
        return Task.FromResult(Result);
    }
}
