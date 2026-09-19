using System.Windows;

using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class MessageBoxConfirmationDialog : IConfirmationDialog
{
    public Task<bool> ConfirmAsync(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
