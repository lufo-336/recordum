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

/// <summary>"Insieme": una riga per figlia sul periodo, disegnata (come <c>HeatGrid</c>). A sinistra la colonna dei nomi
/// (140 px, rientro 12 px per profondità) resta ferma; le celle stanno in uno ScrollViewer proprio, aperto sull'estremo
/// destro (oggi). Fino a tre mesi una cella per giorno, quadrata <c>min(14, spazio ÷ giorni)</c> con un minimo di 4 px (se
/// non ci stanno si scorre). Oltre (B8, scelta dell'autore) una cella per settimana larga fino a riempire la colonna, più
/// piena quanti più giorni fatti sui giorni del periodo; con <see cref="Expanded"/> ("Mostra i giorni") ogni figlia ha la
/// sua mappa 7 × settimane, giorno per giorno. Fatta = colore pieno, prevista e non fatta = <c>Brush.Cell</c>, non
/// prevista = niente, oggi = anello. Sopra: i giorni per un periodo breve, i mesi per uno lungo.</summary>
public sealed class TogetherGrid : Grid
{
    public const double NameColumn = 140, Indent = 12, MaxCell = 14, MaxDayMapCell = 11, MinCell = 4, RowMin = 22,
        HeaderHeight = 18, WeeklyAfterDays = 93;

    public static readonly StyledProperty<IReadOnlyList<TogetherRow>?> RowsProperty =
        AvaloniaProperty.Register<TogetherGrid, IReadOnlyList<TogetherRow>?>(nameof(Rows));

    public static readonly StyledProperty<bool> ExpandedProperty =
        AvaloniaProperty.Register<TogetherGrid, bool>(nameof(Expanded));

    public static readonly DirectProperty<TogetherGrid, bool> WeeklyProperty =
        AvaloniaProperty.RegisterDirect<TogetherGrid, bool>(nameof(Weekly), g => g.Weekly);

    public IReadOnlyList<TogetherRow>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    /// <summary>Un periodo lungo aperto giorno per giorno: una mappa per figlia invece di una riga di settimane.</summary>
    public bool Expanded { get => GetValue(ExpandedProperty); set => SetValue(ExpandedProperty, value); }

    /// <summary>Periodo oltre i tre mesi: le colonne sono settimane (e il pulsante "Mostra i giorni" ha senso).</summary>
    public bool Weekly { get => _weekly; private set => SetAndRaise(WeeklyProperty, ref _weekly, value); }
    private bool _weekly;

    public int RowCount => Rows?.Count ?? 0;

    /// <summary>I giorni del periodo (le celle di una riga).</summary>
    public int Days => Rows is { Count: > 0 } r ? r[0].Cells.Count : 0;

    /// <summary>Le colonne disegnate: i giorni, o le settimane (da lunedì) di un periodo lungo.</summary>
    public int Columns => Weekly ? _weeks : Days;
    private int _weeks;
    private DateOnly _weekStart;

    /// <summary>Lato della cella con la larghezza di adesso: mai sotto <see cref="MinCell"/>.</summary>
    public double CellSize { get; private set; } = MaxCell;

    public double CellGap => Weekly || CellSize >= MaxCell ? 2 : 1;

    public double Pitch => CellSize + CellGap;

    /// <summary>Settimana e mese hanno l'intestazione dei giorni; oltre sono troppo fitti per leggerli.</summary>
    public bool ShowHeader => Days is > 0 and <= 31;

    /// <summary>I mesi sopra le colonne di settimana: la colonna del primo del mese e il nome corto.</summary>
    public IReadOnlyList<(int Column, string Text)> Months { get; private set; } = [];

    /// <summary>La colonna in cui sta il giorno <paramref name="day"/> (indice nelle celle della riga).</summary>
    public int ColumnOf(int day) => !Weekly || Rows is not { Count: > 0 } r ? day : WeekOf(r[0].Cells[day].Date);

    private int WeekOf(DateOnly d) => (d.DayNumber - _weekStart.DayNumber) / 7;

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
        else if (change.Property == BoundsProperty || change.Property == ExpandedProperty) Relayout();
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

    /// <summary>Un solo calcolo di colonne, lato, spazio e altezza di riga, poi si ridisegna e si porta il foglio a oggi.</summary>
    private void Relayout()
    {
        Weekly = Days > WeeklyAfterDays;
        Months = [];
        if (Weekly)
        {
            var cells = Rows![0].Cells;
            _weekStart = cells[0].Date.AddDays(-(((int)cells[0].Date.DayOfWeek + 6) % 7));
            _weeks = WeekOf(cells[^1].Date) + 1;
            var culture = CultureInfo.CurrentCulture;
            Months = [.. cells.Where(c => c.Date.Day == 1)
                .Select(c => (WeekOf(c.Date), culture.DateTimeFormat.GetAbbreviatedMonthName(c.Date.Month).TrimEnd('.')))];
        }
        var room = Math.Max(0, Bounds.Width - NameColumn);
        CellSize = Columns == 0 ? MaxCell
            : Weekly ? Math.Clamp(room / Columns - 2, MinCell, Expanded ? MaxDayMapCell : MaxCell)
            : Math.Clamp(Math.Floor(room / Days), MinCell, MaxCell);
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
        var x = today < 0 ? max : (ColumnOf(today) + 1) * Pitch - Scroll.Viewport.Width;
        Scroll.Offset = new Vector(Math.Clamp(x, 0, max), 0);
    }

    /// <summary>L'etichetta sopra una colonna: in settimana il nome più corto del giorno (in italiano "lun", "mar": tre
    /// lettere che su una cella di 14 px si toccavano), qui ridotto all'iniziale; in mese il numero.</summary>
    internal static string HeaderLabel(DateOnly d, bool week) => week
        ? CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName(d.DayOfWeek)[..1].ToUpper(CultureInfo.CurrentCulture)
        : d.Day.ToString(CultureInfo.CurrentCulture);

    private bool DayMaps => Weekly && Expanded;
    private double RowHeight => DayMaps ? 7 * Pitch + 10 : Math.Max(RowMin, Pitch);
    private double Top => ShowHeader || Weekly ? HeaderHeight : 0;
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
                // Accanto alla sua mappa il nome sta in alto; accanto a una riga, a metà.
                var y = Owner.Top + r * Owner.RowHeight + (Owner.DayMaps ? 0 : (Owner.RowHeight - name.Height) / 2);
                context.DrawText(name, new Point(indent, y));
            }
        }
    }

    private sealed class Cells : Control
    {
        public TogetherGrid Owner = null!;
        private static readonly HeatLevelBrushConverter Heat = new();

        public Cells() => HorizontalAlignment = HorizontalAlignment.Left;

        protected override Size MeasureOverride(Size availableSize) => new(Owner.Columns * Owner.Pitch, Owner.TotalHeight);

        private IBrush? Fill(int step, string hex) =>
            (IBrush?)Heat.Convert([step, hex, ActualThemeVariant], typeof(IBrush), null, CultureInfo.InvariantCulture);

        public override void Render(DrawingContext context)
        {
            if (Owner.Rows is not { Count: > 0 } rows) return;
            var text = Resource(this, "Brush.Text", Brushes.Black);
            var muted = Resource(this, "Brush.TextMuted", Brushes.Gray);
            var empty = Resource(this, "Brush.Cell", Brushes.LightGray);
            var ring = new Pen(text, 1.5);
            var side = Owner.CellSize;
            var pitch = Owner.Pitch;
            var radius = side >= 8 ? 2 : 0;
            var typeface = new Typeface(FontFamily.Default);

            if (Owner.Weekly)
            {
                var free = double.MinValue; // un nome che toccherebbe il precedente (celle strette) si salta
                foreach (var (column, label) in Owner.Months)
                {
                    var t = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, muted);
                    if (column * pitch < free) continue;
                    context.DrawText(t, new Point(column * pitch, 2));
                    free = column * pitch + t.Width + 6;
                }
            }
            else if (Owner.ShowHeader)
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
                var top = Owner.Top + r * Owner.RowHeight;
                var full = Fill(4, row.ColorHex);
                if (Owner.DayMaps) DrawDayMap(context, row, top + 2, full, empty, ring, radius);
                else if (Owner.Weekly) DrawWeeks(context, row, top + (Owner.RowHeight - side) / 2, ring, radius);
                else
                {
                    var y = top + (Owner.RowHeight - side) / 2;
                    for (var i = 0; i < row.Cells.Count; i++)
                    {
                        var cell = row.Cells[i];
                        if (!cell.InRange) continue;
                        var rect = new Rect(i * pitch, y, side, side);
                        context.DrawRectangle(cell.Step >= 4 ? full : empty, null, rect, radius, radius);
                        if (cell.IsToday) context.DrawRectangle(null, ring, rect.Inflate(1), radius + 1, radius + 1);
                    }
                }
            }
        }

        // Una cella per settimana: gradino = giorni fatti sui giorni del periodo (0 = vuota, poi 1–4 nel colore della figlia).
        private void DrawWeeks(DrawingContext context, TogetherRow row, double y, Pen ring, double radius)
        {
            var side = Owner.CellSize;
            foreach (var week in row.Cells.Where(c => c.InRange).GroupBy(c => Owner.WeekOf(c.Date)))
            {
                var n = week.Count();
                var done = week.Count(c => c.Step >= 4);
                var step = done == 0 ? 0 : Math.Clamp((int)Math.Ceiling(4.0 * done / n), 1, 4);
                var rect = new Rect(week.Key * Owner.Pitch, y, side, side);
                context.DrawRectangle(Fill(step, row.ColorHex), null, rect, radius, radius);
                if (week.Any(c => c.IsToday)) context.DrawRectangle(null, ring, rect.Inflate(1), radius + 1, radius + 1);
            }
        }

        // Giorno per giorno: colonne di settimana, righe da lunedì a domenica, come la mappa dell'anno.
        private void DrawDayMap(DrawingContext context, TogetherRow row, double top, IBrush? full, IBrush empty, Pen ring, double radius)
        {
            var side = Owner.CellSize;
            foreach (var cell in row.Cells)
            {
                if (!cell.InRange) continue;
                var y = top + ((int)cell.Date.DayOfWeek + 6) % 7 * Owner.Pitch;
                var rect = new Rect(Owner.WeekOf(cell.Date) * Owner.Pitch, y, side, side);
                context.DrawRectangle(cell.Step >= 4 ? full : empty, null, rect, radius, radius);
                if (cell.IsToday) context.DrawRectangle(null, ring, rect.Inflate(1), radius + 1, radius + 1);
            }
        }
    }
}
