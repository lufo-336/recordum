namespace Recordum.Presentation.Services;

public interface IOpenFileDialog
{
    Task<string?> PromptAsync(string filter);

    /// <summary>Multi-select variant. Returns an empty array (never null) when the user cancels.</summary>
    Task<string[]> PromptMultipleAsync(string filter);
}
