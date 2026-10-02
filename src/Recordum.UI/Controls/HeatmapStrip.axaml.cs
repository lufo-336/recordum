using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Recordum.Core.HomeRow;

namespace Recordum.UI.Controls;

/// <summary>
/// Porting di <c>src/Recordum.WPF/Views/HeatmapStrip.xaml(.cs)</c> (branch avalonia-linux-slice,
/// sonda controlli). Stessa logica di calcolo, riga per riga: due input mode
/// (<see cref="Fractions"/>+<see cref="Kind"/> per il visual per-tipo, <see cref="Counts"/> da
/// solo per il percorso legacy), stessa larghezza fissa <see cref="TotalWidth"/>, stesse cinque
/// forme (Squares/Bars/Band/Dots/Ramp). Il commento sul comportamento resta quello dell'originale
/// WPF — qui si annota solo dove la traduzione ha richiesto una scelta diversa (dettaglio nel
/// report del probe, non ripetuto qui riga per riga).
/// </summary>
public partial class HeatmapStrip : UserControl
{
    private const double TotalWidth = 96;
    private const double TrackHeight = 16;

    // StyledProperty, non DirectProperty: queste quattro proprietà sono pensate per essere
    // impostate DA FUORI (binding da un HomeRow ViewModel, styling, animazione, template) — è
    // esattamente il caso d'uso di StyledProperty in Avalonia. DirectProperty è per proprietà
    // sostenute da un campo CLR e lette/scritte perlopiù dal controllo stesso (in genere per
    // evitare l'overhead della property-value-store quando il valore cambia molto spesso da
    // dentro); non si applica qui, dove il "produttore" del valore è sempre il chiamante.
    public static readonly StyledProperty<ImmutableArray<int>> CountsProperty =
        AvaloniaProperty.Register<HeatmapStrip, ImmutableArray<int>>(nameof(Counts));

    public static readonly StyledProperty<ImmutableArray<double>> FractionsProperty =
        AvaloniaProperty.Register<HeatmapStrip, ImmutableArray<double>>(nameof(Fractions));

    public static readonly StyledProperty<StripStyle> KindProperty =
        AvaloniaProperty.Register<HeatmapStrip, StripStyle>(nameof(Kind), StripStyle.Squares);

    public static readonly StyledProperty<string> AccentColorProperty =
        AvaloniaProperty.Register<HeatmapStrip, string>(nameof(AccentColor), "#3B82F6");

    public ImmutableArray<int> Counts
    {
        get => GetValue(CountsProperty);
        set => SetValue(CountsProperty, value);
    }

    public ImmutableArray<double> Fractions
    {
        get => GetValue(FractionsProperty);
        set => SetValue(FractionsProperty, value);
    }

    public StripStyle Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string AccentColor
    {
        get => GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    // Analogo del callback statico OnChanged passato a PropertyMetadata in WPF: lì il terzo
    // argomento di PropertyMetadata era il callback stesso, qui è l'evento Changed della
    // AvaloniaProperty a cui ci si registra una volta, in blocco, nel costruttore statico.
    static HeatmapStrip()
    {
        CountsProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => strip.Rebuild());
        FractionsProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => strip.Rebuild());
        KindProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => strip.Rebuild());
        AccentColorProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => strip.Rebuild());
    }

    // Sorpresa della porting (dettaglio nel report, sezione "cosa ha sorpreso"): QUI andava
    // chiamato il metodo generato InitializeComponent(), non AvaloniaXamlLoader.Load(this) preso
    // di peso dal boilerplate di una view qualunque. Il generatore Avalonia.Generators
    // (Roslyn incremental generator nel package Avalonia, non XamlIl) produce per ogni x:Name un
    // CAMPO ("Cells") e un metodo InitializeComponent(bool loadXaml = true) che fa DUE cose:
    // AvaloniaXamlLoader.Load(this) *e poi* Cells = FindNameScope().Find&lt;StackPanel&gt;("Cells").
    // Chiamare solo Load(this) esegue la prima metà e salta silenziosamente la seconda: il campo
    // resta null, e non lo si scopre finché non lo si usa (NullReferenceException a runtime, non
    // un errore di compilazione — una view che chiamasse solo Load(this) avrebbe lo stesso
    // problema latente finche' non legge il proprio campo nominato).
    public HeatmapStrip()
    {
        InitializeComponent();
    }

    // Nessun record "Cell" bindabile: i valori calcolati diventano direttamente proprietà di
    // Border, non passano da un DataTemplate (vedi commento in HeatmapStrip.axaml).
    private void Rebuild()
    {
        Cells.Children.Clear();

        if (!Fractions.IsDefaultOrEmpty)
        {
            foreach (var border in BuildStyledCells())
                Cells.Children.Add(border);
            return;
        }
        if (Counts.IsDefault || Counts.Length == 0)
            return;

        foreach (var border in BuildLegacyCells())
            Cells.Children.Add(border);
    }

    private SolidColorBrush Accent(double alpha)
    {
        // ColorConverter.ConvertFromString (WPF) -> Color.Parse (Avalonia): stessa sintassi hex
        // "#RRGGBB" accettata da entrambi, alpha assunto FF quando assente in entrambi i casi.
        // Nessun equivalente di Freeze(): i brush di Avalonia non hanno il pattern Freezable di
        // WPF (ottimizzazione per rendere immutabile e thread-shareable un oggetto altrimenti
        // mutabile) — qui non serve nulla, il brush non viene mai più mutato dopo la creazione.
        var color = Color.Parse(AccentColor);
        return new SolidColorBrush(Color.FromArgb((byte)alpha, color.R, color.G, color.B));
    }

    private List<Border> BuildStyledCells()
    {
        var f = Fractions;
        var n = f.Length;
        var slot = TotalWidth / n;
        var gap = Kind == StripStyle.Band ? 0d : Math.Min(2d, slot * 0.2);
        var w = slot - gap;
        var cells = new List<Border>(n);

        for (var i = 0; i < n; i++)
        {
            var v = f[i];
            var on = v > 0d;
            var margin = new Thickness(0, 0, gap, 0);
            Border cell = Kind switch
            {
                // Counter: barre ancorate in basso, altezza secondo il valore del giorno vs il
                // massimo del periodo.
                StripStyle.Bars => MakeCell(
                    w, on ? Math.Max(3d, v * TrackHeight) : 2d, margin, new CornerRadius(1),
                    VerticalAlignment.Bottom, Accent(on ? 255 : 48)),

                // Journal: un punto nei giorni con una voce, un puntino più fioco altrimenti.
                StripStyle.Dots => MakeCell(
                    on ? 6d : 3d, on ? 6d : 3d,
                    new Thickness((slot - (on ? 6d : 3d)) / 2, 0, (slot - (on ? 6d : 3d)) / 2, 0),
                    new CornerRadius((on ? 6d : 3d) / 2), VerticalAlignment.Center, Accent(on ? 255 : 56)),

                // Resistance: nastro unito sui giorni coperti; i giorni scoperti sono il binario
                // fioco, così le ricadute si leggono come strappi in una banda altrimenti continua.
                StripStyle.Band => MakeCell(
                    slot, 8d, default, BandRadius(f, i),
                    VerticalAlignment.Center, Accent(on ? 230 : 24)),

                // State: celle piene tinte dalla ramp del valore; i giorni senza valore restano fiochi.
                StripStyle.Ramp => MakeCell(
                    w, 12d, margin, new CornerRadius(2),
                    VerticalAlignment.Center, Accent(on ? 96 + v * 159 : 32)),

                // Action (e fallback): quadretti pieni vs vuoti.
                _ => MakeCell(
                    w, Math.Min(w, 10d), margin, new CornerRadius(2),
                    VerticalAlignment.Center, Accent(on ? 255 : 32)),
            };
            cells.Add(cell);
        }
        return cells;
    }

    // Arrotonda solo i bordi esterni di ogni run coperto, così i giorni consecutivi si fondono
    // in un'unica banda. Stessa firma e stessa logica dell'originale WPF; CornerRadius di
    // Avalonia ha lo stesso ordine di argomenti (TopLeft, TopRight, BottomRight, BottomLeft).
    private static CornerRadius BandRadius(ImmutableArray<double> f, int i)
    {
        if (f[i] <= 0d) return default;
        var startOfRun = i == 0 || f[i - 1] <= 0d;
        var endOfRun = i == f.Length - 1 || f[i + 1] <= 0d;
        return new CornerRadius(startOfRun ? 4 : 0, endOfRun ? 4 : 0, endOfRun ? 4 : 0, startOfRun ? 4 : 0);
    }

    private List<Border> BuildLegacyCells()
    {
        var cells = new List<Border>(Counts.Length);
        foreach (var count in Counts)
        {
            var step = count switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => 4 };
            var alpha = step switch { 0 => 32d, 1 => 96d, 2 => 160d, 3 => 208d, _ => 255d };
            cells.Add(MakeCell(10, 10, new Thickness(0, 0, 2, 0), new CornerRadius(2),
                VerticalAlignment.Center, Accent(alpha)));
        }
        return cells;
    }

    private static Border MakeCell(double width, double height, Thickness margin, CornerRadius radius,
        VerticalAlignment vAlign, IBrush brush) => new()
        {
            Width = width,
            Height = height,
            Margin = margin,
            CornerRadius = radius,
            VerticalAlignment = vAlign,
            Background = brush,
        };
}
