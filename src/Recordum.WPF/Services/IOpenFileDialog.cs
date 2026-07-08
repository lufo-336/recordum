namespace Recordum.WPF.Services;

public interface IOpenFileDialog
{
    string? Prompt(string filter);

    /// <summary>Multi-select variant. Returns an empty array (never null) when the user cancels.</summary>
    string[] PromptMultiple(string filter);
}
