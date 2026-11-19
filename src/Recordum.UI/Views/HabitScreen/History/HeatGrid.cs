using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Recordum.UI.Converters;

namespace Recordum.UI.Views.HabitScreen.History;

/// <summary>Una heatmap a colonne di settimana (tre mesi, anno, "Tutto"), disegnata in un solo controllo. Era un pulsante
/// per cella: tre anni erano ~1500 pulsanti e "Tutto" impiegava ~3,5 s a disegnarsi (misurato, onda 2a Task 10). Stesse
/// regole della cella di prima: il gradino nel colore dell'abitudine (HeatLevelBrushConverter), i giorni fuori periodo non
/// si vedono, la selezione è un bordo d'inchiostro, il valore nel tooltip, clic o frecce per scegliere un giorno.
/// B8 (variante A dell'autore): le iniziali dei mesi sopra le colonne, e le celle che si allargano (da <see cref="Cell"/> a
/// <see cref="MaxCell"/>) fino a riempire lo ScrollViewer che la contiene; sotto le 11 px si scorre come prima.</summary>
public sealed class HeatGrid : Control
{
    public const double Cell = 11, MaxCell = 18, Gap = 2, MonthsHeight = 16;

    /// <summary>Lato della cella più lo spazio, con la larghezza di adesso.</summary>
    public double Pitch => _cell + Gap;

    /// <summary>Dove cominciano le celle: sotto le etichette dei mesi.</summary>
    public double Top => Months.Count > 0 ? MonthsHeight : 0;

    /// <summary>Le etichette dei mesi: la colonna della settimana che contiene il primo del mese, e il nome corto.</summary>
    public IReadOnlyList<(int Column, string Text)> Months { get; private set; } = [];

    private double _cell = Cell;
    private ScrollViewer? _host;

    public static readonly StyledProperty<IReadOnlyList<IReadOnlyList<HistoryDayCell>>?> WeeksProperty =
        AvaloniaProperty.Register<HeatGrid, IReadOnlyList<IReadOnlyList<HistoryDayCell>>?>(nameof(Weeks));

    public static readonly StyledProperty<string?> HabitColorProperty =
        AvaloniaProperty.Register<HeatGrid, string?>(nameof(HabitColor));

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<HeatGrid, ICommand?>(nameof(SelectCommand));

    /// <summary>False per la mappa di un gruppo: scegliere un giorno non fa niente, quindi la griglia non lo promette
    /// (niente mano, niente fuoco, niente frecce).</summary>
    public static readonly StyledProperty<bool> SelectableProperty =
        AvaloniaProperty.Register<HeatGrid, bool>(nameof(Selectable), true);

    public bool Selectable { get => GetValue(SelectableProperty); set => SetValue(SelectableProperty, value); }

    public IReadOnlyList<IReadOnlyList<HistoryDayCell>>? Weeks { get => GetValue(WeeksProperty); set => SetValue(WeeksProperty, value); }
    public string? HabitColor { get => GetValue(HabitColorProperty); set => SetValue(HabitColorProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }

    private static readonly HeatLevelBrushConverter Brushes = new();

    static HeatGrid()
    {
        AffectsMeasure<HeatGrid>(WeeksProperty);
        AffectsRender<HeatGrid>(HabitColorProperty);
        FocusableProperty.OverrideDefaultValue<HeatGrid>(true);
        HorizontalAlignmentProperty.OverrideDefaultValue<HeatGrid>(Avalonia.Layout.HorizontalAlignment.Left);
        CursorProperty.OverrideDefaultValue<HeatGrid>(new Cursor(StandardCursorType.Hand));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WeeksProperty)
        {
            Hook(change.OldValue, false);
            if (_attached) Hook(change.NewValue, true);
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty) InvalidateVisual();
        else if (change.Property == SelectableProperty)
        {
            Focusable = Selectable;
            Cursor = Selectable ? new Cursor(StandardCursorType.Hand) : null;
        }
    }

    // Agganciata solo a schermo: Weeks del ViewModel vive quanto la schermata e terrebbe in vita una griglia buttata.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Hook(Weeks, true);
        _host = this.FindAncestorOfType<ScrollViewer>();
        if (_host is not null) _host.PropertyChanged += OnHostChanged;
    }

    // Lo ScrollViewer misura il contenuto con larghezza infinita: la larghezza da riempire è la sua.
    private void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty) InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Hook(Weeks, false);
        if (_host is not null) _host.PropertyChanged -= OnHostChanged;
        _host = null;
    }

    private readonly List<HistoryDayCell> _cells = [];
    private bool _attached;

    // La collezione è la stessa a ogni caricamento (svuotata e riempita): si ascolta anche lei, non solo la proprietà.
    private void Hook(object? weeks, bool on)
    {
        if (weeks is INotifyCollectionChanged incc)
            if (on) incc.CollectionChanged += OnWeeksChanged;
            else incc.CollectionChanged -= OnWeeksChanged;
        Resubscribe(on ? weeks as IReadOnlyList<IReadOnlyList<HistoryDayCell>> : null);
    }

    private void OnWeeksChanged(object? sender, NotifyCollectionChangedEventArgs e) => Resubscribe(Weeks);

    // I marcatori arrivano dopo le celle e la selezione cambia: ogni cella ridisegna la griglia.
    private void Resubscribe(IReadOnlyList<IReadOnlyList<HistoryDayCell>>? weeks)
    {
        foreach (var c in _cells) c.PropertyChanged -= OnCellChanged;
        _cells.Clear();
        if (weeks is not null) _cells.AddRange(weeks.SelectMany(w => w));
        foreach (var c in _cells) c.PropertyChanged += OnCellChanged;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Weeks?.Count ?? 0;
        var room = double.IsFinite(availableSize.Width) ? availableSize.Width : _host?.Bounds.Width ?? 0;
        _cell = count > 0 && room > 0 ? Math.Clamp(room / count - Gap, Cell, MaxCell) : Cell;
        Months = MonthsOf(Weeks);
        return new Size(count * Pitch, Top + 7 * Pitch);
    }

    private static List<(int, string)> MonthsOf(IReadOnlyList<IReadOnlyList<HistoryDayCell>>? weeks)
    {
        var months = new List<(int, string)>();
        if (weeks is null) return months;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        for (var x = 0; x < weeks.Count; x++)
            if (weeks[x].FirstOrDefault(c => c.InRange && c.Date.Day == 1) is { } first)
                months.Add((x, culture.DateTimeFormat.GetAbbreviatedMonthName(first.Date.Month).TrimEnd('.')));
        return months;
    }

    public override void Render(DrawingContext context)
    {
        if (Weeks is not { } weeks) return;
        var theme = ActualThemeVariant;
        var ink = this.TryFindResource("Brush.Ink", theme, out var i) && i is IBrush b ? b : Avalonia.Media.Brushes.Black;
        var selected = new Pen(ink, 2);
        var muted = this.TryFindResource("Brush.TextMuted", theme, out var m) && m is IBrush mb ? mb : Avalonia.Media.Brushes.Gray;
        var typeface = new Typeface(FontFamily.Default);
        var free = double.MinValue; // un nome che toccherebbe il precedente (celle strette) si salta
        foreach (var (column, text) in Months)
        {
            var label = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, 11, muted);
            var x = column * Pitch + Gap / 2;
            if (x < free) continue;
            context.DrawText(label, new Point(x, 0));
            free = x + label.Width + 6;
        }
        var fills = Enumerable.Range(0, 5).Select(step => (IBrush?)Brushes.Convert([step, HabitColor, theme], typeof(IBrush), null,
            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        for (var x = 0; x < weeks.Count; x++)
            for (var y = 0; y < weeks[x].Count; y++)
            {
                var cell = weeks[x][y];
                if (!cell.InRange) continue;
                var fill = fills[Math.Clamp(cell.Step, 0, 4)];
                var rect = new Rect(x * Pitch + Gap / 2, Top + y * Pitch + Gap / 2, _cell, _cell);
                context.DrawRectangle(fill, cell.IsSelected ? selected : null, rect.Deflate(cell.IsSelected ? 1 : 0), 2, 2);
            }
    }

    private HistoryDayCell? CellAt(Point p)
    {
        if (Weeks is not { } weeks) return null;
        if (p.Y < Top) return null;
        var (x, y) = ((int)(p.X / Pitch), (int)((p.Y - Top) / Pitch));
        return x >= 0 && x < weeks.Count && y >= 0 && y < weeks[x].Count && weeks[x][y] is { InRange: true } c ? c : null;
    }

    private void Choose(HistoryDayCell? cell)
    {
        if (!Selectable || cell is null || cell.IsFuture || SelectCommand?.CanExecute(cell) != true) return;
        SelectCommand.Execute(cell);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Choose(CellAt(e.GetPosition(this)));
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        ToolTip.SetTip(this, CellAt(e.GetPosition(this))?.Value);
    }

    // Tastiera: ← → una settimana, ↑ ↓ un giorno, dal giorno scelto (o dall'ultimo del blocco).
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.Key switch { Key.Left => -7, Key.Right => 7, Key.Up => -1, Key.Down => 1, _ => 0 };
        if (step == 0 || !Selectable || Weeks is not { } weeks) return;
        var cells = weeks.SelectMany(w => w).Where(c => c.InRange).ToList();
        if (cells.Count == 0) return;
        var from = cells.FirstOrDefault(c => c.IsSelected)?.Date ?? cells[^1].Date;
        Choose(cells.FirstOrDefault(c => c.Date == from.AddDays(step)));
        e.Handled = true;
    }
}
