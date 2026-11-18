using Avalonia.Controls;
using Avalonia.Input;

namespace Recordum.UI.Behaviors;

/// <summary>Invio in un campo di testo a riga singola lo conferma: si toglie il fuoco, e il binding <c>LostFocus</c> scrive.</summary>
internal static class CommitOnEnter
{
    public static void Watch(Control root)
    {
        // Il fuoco passa alla radice (focalizzabile ma fuori dal giro del Tab).
        root.Focusable = true;
        root.IsTabStop = false;
        root.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && e.Source is TextBox { AcceptsReturn: false }) root.Focus();
        });
    }
}
