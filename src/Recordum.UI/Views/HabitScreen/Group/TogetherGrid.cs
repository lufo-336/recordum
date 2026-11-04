using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Recordum.Presentation.ViewModels.HabitScreen.Group;
using Recordum.UI.Converters;

namespace Recordum.UI.Views.HabitScreen.Group;

/// <summary>"Insieme": una riga per figlia, una cella per giorno del periodo, disegnata (come <c>HeatGrid</c>). A sinistra la
/// colonna dei nomi (140 px, rientro 12 px per profondità) resta ferma; le celle stanno in uno ScrollViewer proprio, aperto
/// sull'estremo destro (oggi). Cella quadrata <c>min(14, spazio ÷ giorni)</c> con un minimo di 4 px: se i giorni non ci stanno
/// si scorre, non si rimpicciolisce (e non si aggrega: ogni cella è un giorno). Fatta = colore pieno, prevista e non fatta =
/// <c>Brush.Cell</c>, non prevista = niente, oggi = anello. L'intestazione dei giorni c'è solo per un periodo breve.</summary>
public sealed class TogetherGrid : Grid
{
    public const double NameColumn = 140, Indent = 12, MaxCell = 14, MinCell = 4, RowMin = 22, HeaderHeight = 18;

    public static readonly StyledProperty<IReadOnlyList<TogetherRow>?> RowsProperty =
        AvaloniaProperty.Register<TogetherGrid, IReadOnlyList<TogetherRow>?>(nameof(Rows));

    public IReadOnlyList<TogetherRow>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    public int RowCount => Rows?.Count ?? 0;

    /// <summary>I giorni del periodo (le celle di una riga).</summary>
    public int Days => Rows is { Count: > 0 } r ? r[0].Cells.Count : 0;

    /// <summary>Lato della cella con la larghezza di adesso: mai sotto <see cref="MinCell"/>.</summary>
    public double CellSize { get; private set; } = MaxCell;

    public double CellGap => CellSize >= MaxCell ? 2 : 1;

    /// <summary>Settimana e mese hanno l'intestazione dei giorni; oltre sono troppo fitti per leggerli.</summary>
    public bool ShowHeader => Days is > 0 and <= 31;

    public ScrollViewer Scroll { get; } = new()
    {
        Name = "TogetherScroll", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
    };

    private readonly Names _names = new();
    private readonly Cells _cells = new();
    private INotifyCollectionChanged? _hooked;

    public TogetherGrid()
    {
        ColumnDefinitions = [new ColumnDefinition(NameColumn, GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star)];
        _names.Owner = _cells.Owner = this;
        Scroll.Content = _cells;
        SetColumn(Scroll, 1);
        Children.Add(_names);
        Children.Add(Scroll);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowsProperty && _attached) { Unhook(); Hook(); Relayout(); }
        else if (change.Property == BoundsProperty) Relayout();
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty) { _names.InvalidateVisual(); _cells.InvalidateVisual(); }
    }

    private bool _attached;

    // Agganciata solo a schermo: la collezione del ViewModel vive quanto la schermata e terrebbe in vita una griglia buttata.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Hook();
        Relayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Unhook();
    }

    private void Hook()
    {
        _hooked = Rows as INotifyCollectionChanged;
        if (_hooked is not null) _hooked.CollectionChanged += OnRowsChanged;
    }

    private void Unhook()
    {
        if (_hooked is not null) _hooked.CollectionChanged -= OnRowsChanged;
        _hooked = null;
    }

    // La collezione è la stessa a ogni caricamento (svuotata e riempita): si ascolta lei, non solo la proprietà.
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Relayout();

    /// <summary>Un solo calcolo di lato, spazio e altezza di riga, poi si ridisegna e si porta il foglio a oggi.</summary>
    private void Relayout()
    {
        var room = Math.Max(0, Bounds.Width - NameColumn);
        CellSize = Days == 0 ? MaxCell : Math.Clamp(Math.Floor(room / Days), MinCell, MaxCell);
        _names.InvalidateMeasure(); _names.InvalidateVisual();
        _cells.InvalidateMeasure(); _cells.InvalidateVisual();
        InvalidateMeasure();
        Dispatcher.UIThread.Post(ScrollToToday, DispatcherPriority.Loaded);
    }

    // Oggi all'estremo destro della finestra (un anno ha i giorni futuri a destra: l'estremo vero sarebbe dicembre).
    // Un periodo passato non ha oggi: si apre sull'estremo destro.
    private void ScrollToToday()
    {
        var max = Math.Max(0, Scroll.Extent.Width - Scroll.Viewport.Width);
        var today = Rows is { Count: > 0 } r ? r[0].Cells.ToList().FindIndex(c => c.IsToday) : -1;
        var x = today < 0 ? max : (today + 1) * (CellSize + CellGap) - Scroll.Viewport.Width;
        Scroll.Offset = new Vector(Math.Clamp(x, 0, max), 0);
    }

    /// <summary>L'etichetta sopra una colonna: in settimana il nome più corto del giorno (in italiano "lun", "mar": tre
    /// lettere che su una cella di 14 px si toccavano), qui ridotto all'iniziale; in mese il numero.</summary>
    internal static string HeaderLabel(DateOnly d, bool week) => week
        ? CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName(d.DayOfWeek)[..1].ToUpper(CultureInfo.CurrentCulture)
        : d.Day.ToString(CultureInfo.CurrentCulture);

    private double RowHeight => Math.Max(RowMin, CellSize + CellGap);
    private double Top => ShowHeader ? HeaderHeight : 0;
    private double TotalHeight => Top + RowCount * RowHeight;

    private static IBrush Resource(Control c, string key, IBrush fallback) =>
        c.TryFindResource(key, c.ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

    private sealed class Names : Control
    {
        public TogetherGrid Owner = null!;
        protected override Size MeasureOverride(Size availableSize) => new(NameColumn, Owner.TotalHeight);

        public override void Render(DrawingContext context)
        {
            if (Owner.Rows is not { Count: > 0 } rows) return;
            var text = Resource(this, "Brush.Text", Brushes.Black);
            var typeface = new Typeface(FontFamily.Default);
            for (var r = 0; r < rows.Count; r++)
            {
                var indent = Math.Min(rows[r].Depth * Indent, NameColumn / 2);
                var name = new FormattedText(rows[r].Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, text)
                {
                    MaxTextWidth = NameColumn - indent - 8, MaxTextHeight = 18, Trimming = TextTrimming.CharacterEllipsis,
                };
                context.DrawText(name, new Point(indent, Owner.Top + r * Owner.RowHeight + (Owner.RowHeight - name.Height) / 2));
            }
        }
    }

    private sealed class Cells : Control
    {
        public TogetherGrid Owner = null!;
        private static readonly HeatLevelBrushConverter Heat = new();

        public Cells() => HorizontalAlignment = HorizontalAlignment.Left;

        protected override Size MeasureOverride(Size availableSize) =>
            new(Owner.Days * (Owner.CellSize + Owner.CellGap), Owner.TotalHeight);

        public override void Render(DrawingContext context)
        {
            if (Owner.Rows is not { Count: > 0 } rows) return;
            var theme = ActualThemeVariant;
            var text = Resource(this, "Brush.Text", Brushes.Black);
            var muted = Resource(this, "Brush.TextMuted", Brushes.Gray);
            var empty = Resource(this, "Brush.Cell", Brushes.LightGray);
            var ring = new Pen(text, 1.5);
            var side = Owner.CellSize;
            var pitch = side + Owner.CellGap;
            var radius = side >= 8 ? 2 : 0;
            var typeface = new Typeface(FontFamily.Default);

            if (Owner.ShowHeader)
            {
                var days = rows[0].Cells;
                var week = days.Count == 7;
                for (var i = 0; i < days.Count; i++)
                {
                    // Settimana: l'iniziale del giorno sopra ogni cella; mese: il numero ogni cinque giorni.
                    var d = days[i].Date;
                    if (!week && d.Day != 1 && d.Day % 5 != 0) continue;
                    var t = new FormattedText(HeaderLabel(d, week), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, muted);
                    context.DrawText(t, new Point(i * pitch + (side - t.Width) / 2, 2));
                }
            }

            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                var y = Owner.Top + r * Owner.RowHeight + (Owner.RowHeight - side) / 2;
                var fill = (IBrush?)Heat.Convert([4, row.ColorHex, theme], typeof(IBrush), null, CultureInfo.InvariantCulture);
                for (var i = 0; i < row.Cells.Count; i++)
                {
                    var cell = row.Cells[i];
                    if (!cell.InRange) continue;
                    var rect = new Rect(i * pitch, y, side, side);
                    context.DrawRectangle(cell.Step >= 4 ? fill : empty, null, rect, radius, radius);
                    if (cell.IsToday) context.DrawRectangle(null, ring, rect.Inflate(1), radius + 1, radius + 1);
                }
            }
        }
    }
}
