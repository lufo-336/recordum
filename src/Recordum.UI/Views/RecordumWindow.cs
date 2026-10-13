using Avalonia.Controls;
using Avalonia.Styling;

namespace Recordum.UI.Views;

/// <summary>Base delle finestre aperte in codice: nessun default dell'app imposta font e fondo, quindi senza questo
/// una finestra modale avrebbe il sans e il fondo Fluent invece di Font.Default e Brush.Background (anche in scuro).
/// Le finestre XAML li impostano a mano (MainWindow, UnlockWindow).</summary>
public class RecordumWindow : Window
{
    protected override Type StyleKeyOverride => typeof(Window);

    public RecordumWindow()
    {
        this[!FontFamilyProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Font.Default");
        this[!BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Brush.Background");
    }
}
