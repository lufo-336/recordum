using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Recordum.UI.Views.HabitBuilder;

/// <summary>L'anteprima viva del builder: tre livelli (riga Home, fascia Oggi, modulo di registrazione) di un
/// <c>BuilderPreviewModel</c>. Inerte: è un'immagine dell'abitudine, nessun controllo reale si può toccare.</summary>
public partial class BuilderPreviewPane : UserControl
{
    public BuilderPreviewPane()
    {
        InitializeComponent();
        // IsHitTestVisible non ferma la tastiera, e uno stile non raggiunge i contenuti costruiti dal template
        // (Space sulla casella "Oggi" lancerebbe ToggleDoneAsync e azzererebbe l'esempio): a ogni layout nessun
        // discendente può prendere il fuoco. Resta l'aspetto abilitato, a differenza di IsEnabled=false.
        Inert.LayoutUpdated += (_, _) =>
        {
            foreach (var c in Inert.GetVisualDescendants().OfType<InputElement>())
                if (c.Focusable || c.IsTabStop) { c.Focusable = false; c.IsTabStop = false; }
        };
    }
}
