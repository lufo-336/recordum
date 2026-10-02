using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Behaviors;

namespace Recordum.UI.Views;

public partial class HomeView : UserControl
{
    private Point? _pressPoint;
    // Il TopLevel va tenuto da parte: allo stacco GetTopLevel(this) è già null e il gestore resterebbe appeso.
    private TopLevel? _top;

    public HomeView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleasedTunnel, RoutingStrategies.Tunnel);
    }

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

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
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
