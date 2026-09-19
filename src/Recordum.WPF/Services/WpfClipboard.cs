using Recordum.Presentation.Services;

namespace Recordum.WPF.Services;

public sealed class WpfClipboard : IClipboard
{
    public void SetText(string text) => System.Windows.Clipboard.SetText(text);
}
