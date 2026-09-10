using Avalonia.Controls;
using Recordum.UI.Controls;
using Recordum.Core.HomeRow;

namespace Recordum.UI.Views;

/// <summary>
/// Sonda controlli (branch avalonia-linux-slice): esercita <see cref="HeatmapStrip"/> nelle sue
/// cinque forme per-tipo (deliverable #2 del probe controlli). NON è una vista di produzione:
/// nessun'altra parte dell'app la referenzia (stesso principio isolante di ThemeProbeView).
/// </summary>
public partial class HeatmapStripProbeView : UserControl
{
    // InitializeComponent(), non AvaloniaXamlLoader.Load(this): i cinque ContentControl nominati
    // (ActionSlot/CounterSlot/...) sono cablati dal generatore SOLO dentro InitializeComponent()
    // — vedi il commento gemello in Controls/HeatmapStrip.axaml.cs e il report del probe.
    public HeatmapStripProbeView()
    {
        InitializeComponent();

        // Valori realistici, non uniformi: le mini-barre del Counter e la ramp dello State
        // esistono per mostrare la MAGNITUDINE, quindi una fraction piatta non proverebbe nulla
        // (vedi brief). Il numero di giorni segue HomeRowPresentation.ResolveStrip: 14 per
        // Resistance, 7 per gli altri quattro tipi.
        ActionSlot.Content = new HeatmapStrip
        {
            Kind = StripStyle.Squares,
            Fractions = [1, 0, 1, 1, 1, 0, 1],
        };

        CounterSlot.Content = new HeatmapStrip
        {
            Kind = StripStyle.Bars,
            Fractions = [0.2, 0.5, 0d, 0.8, 1.0, 0.35, 0.65],
        };

        ResistanceSlot.Content = new HeatmapStrip
        {
            Kind = StripStyle.Band,
            Fractions = [1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1],
        };

        JournalSlot.Content = new HeatmapStrip
        {
            Kind = StripStyle.Dots,
            Fractions = [1, 0, 1, 1, 0, 0, 1],
        };

        StateSlot.Content = new HeatmapStrip
        {
            Kind = StripStyle.Ramp,
            Fractions = [0.1, 0.9, 0.5, 0d, 0.7, 0.3, 1.0],
        };
    }
}
