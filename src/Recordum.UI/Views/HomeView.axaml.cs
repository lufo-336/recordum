using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Recordum.UI.Behaviors;
using Recordum.UI.Controls;

namespace Recordum.UI.Views;

public partial class HomeView : UserControl
{
    public static readonly FuncValueConverter<HomeSort, string> SortLabel = new(s => s switch
    {
        HomeSort.Name => Strings.Home_Filter_Sort_Name,
        HomeSort.LastCompletion => Strings.Home_Filter_Sort_LastCompletion,
        _ => Strings.Home_Filter_Sort_Manual,
    });

    private Point? _pressPoint;
    // Il TopLevel va tenuto da parte: allo stacco GetTopLevel(this) è già null e il gestore resterebbe appeso.
    private TopLevel? _top;
    private bool _searchOpen;

    public HomeView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleasedTunnel, RoutingStrategies.Tunnel);
        PropertyChanged += (_, e) => { if (e.Property == BoundsProperty) ApplyWidthRules(); };
        DataContextChanged += (_, _) => WatchStripLength();
        SearchBox.LostFocus += (_, _) => { if (string.IsNullOrEmpty(SearchBox.Text)) CloseSearch(); };
    }

    // I giorni visibili li dice HomeRowsMetrics.VisibleDays; sotto i 320 px la data accanto a "Oggi" si nasconde,
    // sotto i 400 si stringe la ricerca e sparisce il testo di "Fatte". L'intestazione dei giorni non è più lunga della
    // striscia più lunga fra le righe.
    internal const double FourteenDaysFrom = 640;
    internal const double SevenDaysFrom = 400;
    internal const double DateFrom = 320;

    private HomeViewModel? _watched;

    private void WatchStripLength()
    {
        if (_watched is not null) _watched.PropertyChanged -= OnHomePropertyChanged;
        _watched = DataContext as HomeViewModel;
        if (_watched is not null) _watched.PropertyChanged += OnHomePropertyChanged;
        ApplyRowsStyle();
    }

    private void ApplyRowsStyle()
    {
        HomeRowsMetrics.SetRows(this, (DataContext as HomeViewModel)?.RowsStyle ?? HomeRows.Comfortable);
        ApplyWidthRules();
    }

    private void OnHomePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HomeViewModel.RowsStyle):
                ApplyRowsStyle();
                break;
            case nameof(HomeViewModel.MaxVisibleStripLength):
            case nameof(HomeViewModel.ActiveTab):
            case nameof(HomeViewModel.DateLabel):
            case nameof(HomeViewModel.DayCountText):
                ApplyWidthRules();
                break;
            case nameof(HomeViewModel.SearchText):
                // Il cambio di scheda azzera i filtri: se la casella è vuota e non ha il fuoco, si richiude.
                if (_searchOpen && string.IsNullOrEmpty(_watched?.SearchText) && !SearchBox.IsKeyboardFocusWithin)
                    CloseSearch();
                break;
        }
    }

    private void ApplyWidthRules()
    {
        var width = Bounds.Width;
        if (width <= 0) return;
        var vm = DataContext as HomeViewModel;
        var style = HomeRowsMetrics.GetRows(this);
        var days = HomeRowsMetrics.VisibleDays(style, width);
        HeatmapStrip.SetVisibleDays(this, days);
        var longest = vm?.MaxVisibleStripLength ?? 0;
        var headerDays = Math.Max(1, Math.Min(days, longest > 0 ? longest : days));
        HeatmapStrip.SetVisibleDays(DayHeader, headerDays);
        DayHeader.Margin = new Thickness(0, 0, HomeRowsMetrics.For(style).Command + 15, 2);
        // Con zero righe non si tocca: una ricerca senza risultati non deve riportare a oggi.
        if (vm is not null && longest > 0) vm.MaxDaysBack = headerDays - 1;

        var active = vm is null || vm.ActiveTab == HomeTab.Active;
        TodayDate.IsVisible = !_searchOpen && width >= DateFrom && !string.IsNullOrEmpty(vm?.DateLabel);
        DayCount.IsVisible = !_searchOpen && !string.IsNullOrEmpty(vm?.DayCountText);
        SearchBox.IsVisible = _searchOpen;
        SearchBox.Width = width >= SevenDaysFrom ? 200 : 120;
        BackToActiveButton.IsVisible = !active;
        DoneLabel.IsVisible = width >= SevenDaysFrom;
    }

    private void OpenSearch()
    {
        _searchOpen = true;
        ApplyWidthRules();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void CloseSearch()
    {
        _searchOpen = false;
        // Una ricerca che non si vede non deve filtrare.
        if (DataContext is HomeViewModel vm && !string.IsNullOrEmpty(vm.SearchText))
            vm.SearchText = null;
        ApplyWidthRules();
    }

    private void OnSearchButtonClick(object? sender, RoutedEventArgs e) => OpenSearch();

    // Le scorciatoie della Home (Ctrl+F e i tasti di riga, D5) non possono stare su HomeView: un
    // evento di tastiera viaggia dalla radice al SOLO elemento a fuoco, quindi finché il fuoco non è
    // dentro la Home — appena aperta non lo è — HomeView non è sul percorso e non vede niente.
    // Si ascolta quindi sul TopLevel e si rinuncia quando il fuoco è su un elemento che NON discende
    // da questa view (tipicamente un tab del workspace): "nessun fuoco" e "fuoco nella Home"
    // funzionano, "fuoco altrove" non viene rubato.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        _top?.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _top?.RemoveHandler(KeyDownEvent, OnKeyDownTunnel);
        _top = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
        {
            _pressPoint = e.GetPosition(this);
            return;
        }

        // Tasto destro su una riga FUORI dalla selezione: la selezione collassa su quella riga,
        // come nei gestori di file, così il menu agisce su ciò che l'utente vede evidenziato.
        // In Avalonia il menu contestuale si apre al RILASCIO, quindi qui si è ancora in tempo.
        if (props.IsRightButtonPressed && FindRow(e.Source) is { IsSelected: false } row)
        {
            home.SelectRowCommand.Execute(row);
        }
    }

    private void OnPointerReleasedTunnel(object? sender, PointerReleasedEventArgs e)
    {
        var press = _pressPoint;
        _pressPoint = null;
        if (DataContext is not HomeViewModel home) return;
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        // Un press-drag non è un clic (D4): oltre la soglia del trascinamento non si seleziona.
        if (press is { } p && Distance(p, e.GetPosition(this)) >= TreeDragDropBehavior.DragThreshold) return;

        var (row, interactive) = Probe(e.Source);
        if (interactive) return;
        if (row is not null)
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (ctrl || shift) home.SelectInteractive(row, ctrl, shift);
            else home.SelectRowCommand.Execute(row);
            return;
        }

        // Clic nello spazio vuoto dell'albero: si torna alla Home a tutta larghezza (Modello A) —
        // deseleziona e disattiva il tab. Scatta anche senza righe selezionate, perché un tab può
        // essere attivo mentre SelectedRow è null. Limitato alla regione albero, così la barra
        // superiore e la riga dei filtri non lo fanno scattare.
        if (IsDescendantOfTreeRegion(e.Source)) home.ClickBackground();
    }

    private static double Distance(Point a, Point b)
    {
        var d = a - b;
        return Math.Sqrt(d.X * d.X + d.Y * d.Y);
    }

    private bool IsDescendantOfTreeRegion(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
            if (ReferenceEquals(v, TreeRegion)) return true;
        return false;
    }

    /// <summary>Risale l'albero visuale una volta sola e risponde a due domande: quale riga sta
    /// sotto il punto, e se per arrivarci si è passati da un controllo interattivo (un pulsante, la
    /// casella di rinomina, una voce di menu). Nota di API: Avalonia 12 NON ha più `ButtonBase` —
    /// `Button` è la base di ToggleButton, RadioButton e CheckBox, quindi `is Button` li copre tutti.</summary>
    private static (HomeRowViewModel? Row, bool Interactive) Probe(object? source)
    {
        var interactive = false;
        HomeRowViewModel? found = null;
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
        {
            if (v is TextBox or Button or MenuItem) interactive = true;
            // NON si esce alla prima riga trovata: le parti interne del template di un TextBox
            // ereditano il DataContext della riga, quindi uscire lì dichiarerebbe "riga, non
            // interattivo" per un clic dentro la casella di rinomina (misurato). Si risale fino in
            // cima: sopra una riga non c'è nessun pulsante, quindi non ci sono falsi positivi.
            if (found is null && v is StyledElement { DataContext: HomeRowViewModel row }) found = row;
        }
        return (found, interactive);
    }

    private static HomeRowViewModel? FindRow(object? source) => Probe(source).Row;

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (DataContext is not HomeViewModel home) return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is Visual fv && !this.IsVisualAncestorOf(fv)) return;

        if (e.Key == Key.Escape && _searchOpen && SearchBox.IsKeyboardFocusWithin)
        {
            CloseSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            OpenSearch();
            e.Handled = true;
            return;
        }

        // D5: i tasti di riga agiscono sulla riga SELEZIONATA, non su quella che ha il fuoco —
        // in WPF funzionavano solo se il fuoco era su un discendente focusabile della riga.
        if (focused is TextBox) return;
        if (home.SelectedRow is not { } row) return;
        DispatchRowKey(row, e);
    }

    private static void DispatchRowKey(HomeRowViewModel row, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                if (row.BeginRenameCommand.CanExecute(null)) row.BeginRenameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete:
                if (row.SoftDeleteCommand.CanExecute(null)) row.SoftDeleteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.E when e.KeyModifiers == KeyModifiers.Control:
                var toggle = row.IsArchived ? row.UnarchiveCommand : row.ArchiveCommand;
                if (toggle.CanExecute(null)) toggle.Execute(null);
                e.Handled = true;
                break;
            case Key.R when e.KeyModifiers == KeyModifiers.Control:
                if (row.RestoreCommand.CanExecute(null)) row.RestoreCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}

/// <summary>Dispone il blocco titolo a sinistra e gli strumenti a destra su una riga finché ci stanno, poi manda gli
/// strumenti sotto allineati a destra.</summary>
public sealed class HomeHeadPanel : Panel
{
    private const double ColumnGap = 8;
    private const double RowGap = 6;
    private bool _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2) return base.MeasureOverride(availableSize);
        var lead = Children[0];
        var tools = Children[1];
        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        lead.Measure(unbounded);
        tools.Measure(unbounded);

        var oneRowWidth = lead.DesiredSize.Width + ColumnGap + tools.DesiredSize.Width;
        _wrapped = !double.IsInfinity(availableSize.Width) && oneRowWidth > availableSize.Width;
        if (!_wrapped)
        {
            return new Size(oneRowWidth, Math.Max(lead.DesiredSize.Height, tools.DesiredSize.Height));
        }

        // Rimisura il blocco sinistro con la larghezza disponibile così il titolo si accorcia con i puntini.
        lead.Measure(new Size(availableSize.Width, availableSize.Height));
        tools.Measure(new Size(availableSize.Width, availableSize.Height));   // sotto i 340 px gli strumenti vanno a capo fra loro
        var width = Math.Min(availableSize.Width, Math.Max(lead.DesiredSize.Width, tools.DesiredSize.Width));
        return new Size(width, lead.DesiredSize.Height + RowGap + tools.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 2) return base.ArrangeOverride(finalSize);
        var lead = Children[0];
        var tools = Children[1];

        if (!_wrapped)
        {
            var leadY = (finalSize.Height - lead.DesiredSize.Height) / 2;
            var toolsY = (finalSize.Height - tools.DesiredSize.Height) / 2;
            lead.Arrange(new Rect(0, leadY, lead.DesiredSize.Width, lead.DesiredSize.Height));
            tools.Arrange(new Rect(finalSize.Width - tools.DesiredSize.Width, toolsY, tools.DesiredSize.Width, tools.DesiredSize.Height));
        }
        else
        {
            lead.Arrange(new Rect(0, 0, Math.Min(lead.DesiredSize.Width, finalSize.Width), lead.DesiredSize.Height));
            var toolsX = Math.Max(0, finalSize.Width - tools.DesiredSize.Width);
            tools.Arrange(new Rect(toolsX, lead.DesiredSize.Height + RowGap, tools.DesiredSize.Width, tools.DesiredSize.Height));
        }
        return finalSize;
    }
}
