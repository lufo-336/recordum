namespace Recordum.Presentation.Services;

/// <summary>Abstraction over the system clipboard, for testability.</summary>
public interface IClipboard
{
    void SetText(string text);
}
