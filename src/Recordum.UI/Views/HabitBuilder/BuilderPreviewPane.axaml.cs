using Avalonia.Controls;

namespace Recordum.UI.Views.HabitBuilder;

/// <summary>L'anteprima viva del builder: tre livelli (riga Home, fascia Oggi, modulo di registrazione) di un
/// <c>BuilderPreviewModel</c>. Inerte: è un'immagine dell'abitudine, nessun controllo reale si può toccare.</summary>
public partial class BuilderPreviewPane : UserControl
{
    public BuilderPreviewPane() => InitializeComponent();
}
