namespace Recordum.WPF.Services;

/// <summary>Yes/No confirmation prompt. Abstracted so delete flows stay unit-testable.</summary>
public interface IConfirmationDialog
{
    Task<bool> ConfirmAsync(string title, string message);
}
