using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Recordum.Core.HomeRow;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Converters;
using Recordum.UI.Theming;

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

    /// <summary>Le celle da scrivere (la striscia della Home): quando ci sono, la striscia è testo e non forme.</summary>
    public static readonly StyledProperty<ImmutableArray<StripCell>> ValuesProperty =
        AvaloniaProperty.Register<HeatmapStrip, ImmutableArray<StripCell>>(nameof(Values));

    public ImmutableArray<StripCell> Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Quanti giorni mostrano le strisce di valori sotto questo elemento: lo decide la Home da larghezza e versione
    /// delle righe (<see cref="HomeRowsMetrics.VisibleDays"/>) e lo eredita ogni riga, così intestazione e strisce hanno le stesse colonne.</summary>
    public static readonly AttachedProperty<int> VisibleDaysProperty =
        AvaloniaProperty.RegisterAttached<HeatmapStrip, Control, int>("VisibleDays", 7, inherits: true);

    public static int GetVisibleDays(Control element) => element.GetValue(VisibleDaysProperty);
    public static void SetVisibleDays(Control element, int value) => element.SetValue(VisibleDaysProperty, value);

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
        ValuesProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => strip.Rebuild());
        VisibleDaysProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => { if (!strip.Values.IsDefaultOrEmpty) strip.Rebuild(); });
        HomeRowsMetrics.RowsProperty.Changed.AddClassHandler<HeatmapStrip>((strip, _) => { if (!strip.Values.IsDefaultOrEmpty) strip.Rebuild(); });
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
        // In "Segni" il contorno del giorno scelto sporge di 3 px dal quadratino, anche fuori dalla striscia: tagliato,
        // se ne vedevano solo i lati (PNG del Task 9).
        ClipToBounds = false;
        Cells.ClipToBounds = false;
        // La variante "testo" dei valori dipende dal tema: un cambio a runtime ridisegna la striscia (come StreakTimeline).
        ActualThemeVariantChanged += (_, _) => { if (!Values.IsDefaultOrEmpty) Rebuild(); };
    }

    /// <summary>Quante volte le celle sono state ricostruite (solo per i test: lo scopo e' non farle ricostruire a vuoto).</summary>
    internal int RebuildCount { get; private set; }

    // Nessun record "Cell" bindabile: i valori calcolati diventano direttamente proprietà di
    // Border, non passano da un DataTemplate (vedi commento in HeatmapStrip.axaml).
    // I binding impostano Kind, Fractions, Values e AccentColor uno dopo l'altro: ricostruire a ogni proprieta' le
    // disegnava fino a cinque volte. Le proprieta' segnano la striscia da rifare, la misura la rifa' una volta.
    private bool _dirty = true;

    private void Rebuild()
    {
        _dirty = true;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_dirty)
        {
            _dirty = false;
            BuildCells();
        }
        return base.MeasureOverride(availableSize);
    }

    private void BuildCells()
    {
        RebuildCount++;
        Cells.Children.Clear();
        Cells.Spacing = 0;

        if (!Values.IsDefaultOrEmpty)
        {
            foreach (var cell in BuildValueCells())
                Cells.Children.Add(cell);
            return;
        }
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

    internal const string SelectedCellName = "SelectedDay";

    // La striscia della Home (direzione visiva §5): i valori del giorno nella variante "testo" del colore dell'abitudine,
    // i giorni vuoti nel testo tenue. Ogni cella è larga uguale, così oggi resta sotto "oggi" dell'intestazione.
    private List<Control> BuildValueCells()
    {
        var metrics = HomeRowsMetrics.Of(this);
        Cells.Spacing = metrics.Gap;

        var shown = Math.Min(Values.Length, Math.Max(1, GetVisibleDays(this)));
        var app = global::Avalonia.Application.Current!;
        IBrush Token(string key) => HabitTextBrushConverter.Resource<IBrush>(app, key, ActualThemeVariant) ?? Brushes.Gray;
        var paneBrush = HabitTextBrushConverter.Resource<ISolidColorBrush>(app, "Brush.Pane", ActualThemeVariant);
        if (metrics.Marks)
            return BuildMarkCells(metrics, shown, Token("Brush.CellEmpty"), Token("Brush.Ink"), paneBrush?.Color ?? Colors.White);

        var values = Values[^shown..];   // oggi è l'ultima: si tengono i giorni più recenti
        var on = HabitTextBrushConverter.BrushFor(AccentColor, ActualThemeVariant);
        var off = Token("Brush.TextFaint");
        // Fondo opaco sul pannello per il giorno scelto, così resta leggibile anche con la tinta della riga.
        IBrush? selectedBg = null;
        if (HabitColors.TryParse(AccentColor, out var habit) && paneBrush is { } pane)
        {
            var opacity = ActualThemeVariant == ThemeVariant.Dark ? 0.36 : 0.26;
            selectedBg = new ImmutableSolidColorBrush(HabitColors.Blend(habit, opacity, pane.Color));
        }

        return values.Select<StripCell, Control>(cell =>
        {
            var text = new TextBlock
            {
                Text = cell.Glyph,
                Width = metrics.Column,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.None,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13d,
                FontWeight = cell.On ? FontWeight.SemiBold : FontWeight.Normal,
                FontFeatures = new FontFeatureCollection { FontFeature.Parse("+tnum") },
                Foreground = cell.On ? on : off,
            };
            if (!cell.IsSelected) return text;
            return new Border
            {
                Name = SelectedCellName,
                Width = metrics.Column,
                Height = 24d,
                CornerRadius = new CornerRadius(5),
                VerticalAlignment = VerticalAlignment.Center,
                Background = selectedBg,
                Child = text,
            };
        }).ToList();
    }

    private List<Control> BuildMarkCells(HomeRowsMetrics metrics, int shown, IBrush empty, IBrush ink, Color pane)
    {
        var offset = Values.Length - shown;
        var hasFractions = !Fractions.IsDefault && Fractions.Length == Values.Length;
        var habit = HabitColors.TryParse(AccentColor, out var parsed) ? parsed : Colors.Gray;
        var cells = new List<Control>(shown);

        for (var i = 0; i < shown; i++)
        {
            var cell = Values[offset + i];
            IBrush fill;
            if (!cell.On)
            {
                fill = empty;
            }
            else
            {
                var fraction = hasFractions ? Fractions[offset + i] : 1d;
                var level = fraction <= 1d / 3d ? 0.35 : fraction <= 2d / 3d ? 0.65 : 1d;
                fill = new ImmutableSolidColorBrush(HabitColors.Blend(habit, level, pane));
            }

            var square = new Border
            {
                Width = metrics.Column,
                Height = metrics.Column,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = fill,
            };

            if (!cell.IsSelected)
            {
                cells.Add(square);
                continue;
            }

            var outline = new Border
            {
                Name = SelectedCellName,
                Width = metrics.Column + 6,
                Height = metrics.Column + 6,
                Margin = new Thickness(-3),
                BorderThickness = new Thickness(2),
                BorderBrush = ink,
                CornerRadius = new CornerRadius(6),
                IsHitTestVisible = false,
            };
            var host = new Panel
            {
                Width = metrics.Column,
                Height = metrics.Column,
                VerticalAlignment = VerticalAlignment.Center,
            };
            host.Children.Add(square);
            host.Children.Add(outline);
            cells.Add(host);
        }

        return cells;
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
