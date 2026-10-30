using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen.Group;
using Recordum.UI.Converters;

namespace Recordum.UI.Views.HabitScreen.Group;

/// <summary>"Insieme": una riga per figlia, una cella per giorno del periodo, in un solo controllo (come <c>HeatGrid</c>).
/// Il nome sta in una colonna di 140 px (rientro 12 px per profondità); le celle sono quadrate, <c>min(14, spazio ÷ giorni)</c>,
/// con 2 px di spazio (sotto i 4 px niente). Fatta = colore pieno, prevista e non fatta = <c>Brush.Cell</c>, non prevista =
/// niente, oggi = anello. L'intestazione dei giorni c'è solo per un periodo breve (settimana, mese).</summary>
public sealed class TogetherGrid : Control
{
    public const double NameColumn = 140, Indent = 12, MaxCell = 14, RowMin = 22, HeaderHeight = 18;

    public static readonly StyledProperty<IReadOnlyList<TogetherRow>?> RowsProperty =
        AvaloniaProperty.Register<TogetherGrid, IReadOnlyList<TogetherRow>?>(nameof(Rows));

    public IReadOnlyList<TogetherRow>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    public int RowCount => Rows?.Count ?? 0;

    /// <summary>I giorni del periodo (le celle di una riga).</summary>
    public int Days => Rows is { Count: > 0 } r ? r[0].Cells.Count : 0;

    /// <summary>Lato della cella al lavoro con la larghezza di adesso (frazionario: un anno in 460 px sono linee sottili, ma visibili).</summary>
    public double CellSize => Days == 0 ? 0 : Math.Min(MaxCell, Math.Max(0, Bounds.Width - NameColumn) / Days);

    public double CellGap => CellSize < 4 ? 0 : 2;

    /// <summary>Settimana e mese hanno l'intestazione dei giorni; tre mesi e anno sono troppo fitti per leggerli.</summary>
    public bool ShowHeader => Days is > 0 and <= 31;

    private static readonly HeatLevelBrushConverter Brushes = new();

    static TogetherGrid()
    {
        AffectsMeasure<TogetherGrid>(RowsProperty);
        AffectsRender<TogetherGrid>(RowsProperty);
        HorizontalAlignmentProperty.OverrideDefaultValue<TogetherGrid>(Avalonia.Layout.HorizontalAlignment.Stretch);
    }

    private INotifyCollectionChanged? _hooked;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowsProperty)
        {
            if (_hooked is not null) _hooked.CollectionChanged -= OnRowsChanged;
            _hooked = change.NewValue as INotifyCollectionChanged;
            if (_hooked is not null) _hooked.CollectionChanged += OnRowsChanged;
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty) InvalidateVisual();
    }

    // La collezione è la stessa a ogni caricamento (svuotata e riempita): si ascolta lei, non solo la proprietà.
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private double RowHeight => Math.Max(RowMin, CellSize + CellGap);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? NameColumn + 7 * MaxCell : availableSize.Width;
        // CellSize legge Bounds: qui il lato si ricalcola dalla larghezza offerta.
        var side = Days == 0 ? 0 : Math.Min(MaxCell, Math.Max(0, width - NameColumn) / Days);
        var row = Math.Max(RowMin, side + (side < 4 ? 0 : 2));
        return new Size(width, (ShowHeader ? HeaderHeight : 0) + RowCount * row);
    }

    private IBrush Resource(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

    public override void Render(DrawingContext context)
    {
        if (Rows is not { Count: > 0 } rows) return;
        var theme = ActualThemeVariant;
        var text = Resource("Brush.Text", Avalonia.Media.Brushes.Black);
        var muted = Resource("Brush.TextMuted", Avalonia.Media.Brushes.Gray);
        var empty = Resource("Brush.Cell", Avalonia.Media.Brushes.LightGray);
        var ring = new Pen(text, 1.5);
        var side = CellSize;
        var pitch = side + CellGap;
        var top = ShowHeader ? HeaderHeight : 0;
        var typeface = new Typeface(FontFamily.Default);

        if (ShowHeader)
        {
            var cells = rows[0].Cells;
            var week = cells.Count == 7;
            for (var i = 0; i < cells.Count; i++)
            {
                // Settimana: l'iniziale del giorno sopra ogni cella; mese: il numero ogni cinque giorni (a 10 px non ci stanno tutti).
                var d = cells[i].Date;
                if (!week && d.Day != 1 && d.Day % 5 != 0) continue;
                var label = week ? CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName(d.DayOfWeek)
                                 : d.Day.ToString(CultureInfo.CurrentCulture);
                var t = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, muted);
                context.DrawText(t, new Point(NameColumn + i * pitch + (side - t.Width) / 2, 2));
            }
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var y = top + r * RowHeight;
            var indent = Math.Min(row.Depth * Indent, NameColumn / 2);
            var name = new FormattedText(row.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 13, text)
            {
                MaxTextWidth = NameColumn - indent - 8, MaxTextHeight = 18, Trimming = TextTrimming.CharacterEllipsis,
            };
            context.DrawText(name, new Point(indent, y + (RowHeight - name.Height) / 2));
            var fill = (IBrush?)Brushes.Convert([4, row.ColorHex, theme], typeof(IBrush), null, CultureInfo.InvariantCulture);
            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cell = row.Cells[i];
                if (!cell.InRange) continue;
                var rect = new Rect(NameColumn + i * pitch, y + (RowHeight - side) / 2, side, side);
                context.DrawRectangle(cell.Step >= 4 ? fill : empty, null, rect, side >= 4 ? 2 : 0, side >= 4 ? 2 : 0);
                if (cell.IsToday) context.DrawRectangle(null, ring, rect.Inflate(side >= 4 ? 1 : 0), side >= 4 ? 3 : 0, side >= 4 ? 3 : 0);
            }
        }
    }
}
