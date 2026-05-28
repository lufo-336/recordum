namespace Recordum.WPF.Services;

public interface IOpenFileDialog
{
    string? Prompt(string filter);
}
