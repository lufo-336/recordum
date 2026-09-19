using Recordum.Presentation.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeConfirmationDialog : IConfirmationDialog
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}
