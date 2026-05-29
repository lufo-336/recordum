namespace Recordum.WPF.Services;

/// <summary>Abstraction over <see cref="System.Windows.Clipboard"/> for testability.</summary>
public interface IClipboard
{
    void SetText(string text);
}

public sealed class WpfClipboard : IClipboard
{
    public void SetText(string text) => System.Windows.Clipboard.SetText(text);
}
