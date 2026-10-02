using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Workspace;

namespace Recordum.UI.Views;

public partial class MainWindow : Window
{
    // Glifi della didascalia: un quadrato per massimizzare, due quadrati sfalsati per ripristinare.
    private static readonly Geometry MaximizeGlyph = Geometry.Parse("M5 5h14v14H5z");
    private static readonly Geometry RestoreGlyph = Geometry.Parse("M8 5h11v11 M5 8h11v11H5z");

    private readonly MainWindowViewModel _viewModel;

    // Serve solo per il pubblico designer/preview di Avalonia e per i test che costruiscono la
    // finestra senza contenitore: la strada vera e' il costruttore con il ViewModel.
    public MainWindow() : this(null!) { }

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        // InitializeComponent(), NON AvaloniaXamlLoader.Load(this): senza di esso i campi x:Name
        // restano null e il guasto arriva a runtime, muto.
        InitializeComponent();
        FitToScreen();
        if (viewModel is not null) DataContext = viewModel;

        MinButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxButton.Click += (_, _) => ToggleMaximize();
        CloseButton.Click += (_, _) => Close();
        SidebarSplitter.DragCompleted += OnSidebarSplitterDragCompleted;

        // Le scorciatoie che devono vincere sul focus corrente viaggiano in tunnel, come il
        // PreviewKeyDown di WPF. handledEventsToo: true NON e' decorativo — le KeyBindings della
        // finestra vengono valutate PRIMA del giro dell'evento e marcano il tasto come gestito,
        // quindi un gestore in tunnel senza questa opzione non viene mai chiamato (misurato: la
        // scheda cambiava ma il workspace non tornava alla Home).
        AddHandler(KeyDownEvent, OnWindowKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += OnOpened;
        Closed += OnClosed;
    }

    /// <summary>La finestra non si apre mai piu' grande dell'area di lavoro dello schermo: il
    /// 1280x760 di default sfondava i display piccoli e spingeva fuori schermo la barra in basso.
    /// <see cref="Screens"/> risponde gia' nel costruttore (verificato su X11), quindi si fa qui
    /// come faceva WPF, prima che la finestra si veda.</summary>
    private void FitToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        // WorkingArea e' in pixel fisici: a scaling 2 un'area di 3840 vale 1920 DIP.
        var scale = screen.Scaling <= 0 ? 1d : screen.Scaling;
        var waWidth = screen.WorkingArea.Width / scale;
        var waHeight = screen.WorkingArea.Height / scale;
        (Width, Height) = ClampToWorkArea(Width, Height, waWidth, waHeight);
        MinWidth = Math.Min(MinWidth, waWidth);
        MinHeight = Math.Min(MinHeight, waHeight);
    }

    /// <summary>Il glifo del pulsante ingrandisci/ripristina, come funzione pura dello stato.
    /// Vale la pena estrarla: il confronto per identita' dell'istanza e' l'unico modo onesto di
    /// provare lo scambio, e il valore iniziale che arriva dallo XAML e' una TERZA istanza.</summary>
    internal static Geometry GlyphFor(WindowState state)
        => state == WindowState.Maximized ? RestoreGlyph : MaximizeGlyph;

    /// <summary>Limite puro: mai piu' grande dell'area di lavoro. Provato a parte.</summary>
    internal static (double Width, double Height) ClampToWorkArea(
        double desiredWidth, double desiredHeight, double workAreaWidth, double workAreaHeight)
        => (Math.Min(desiredWidth, workAreaWidth), Math.Min(desiredHeight, workAreaHeight));

    /// <summary>Le tre decisioni di colonna della Home, in una funzione pura: quanto larga la
    /// barra laterale, quanto largo il workspace, e se il divisore si vede. Sta qui e non in un
    /// ViewModel perche' <see cref="GridLength"/> e' un tipo d'interfaccia e
    /// <c>Recordum.Presentation</c> non puo' nominarlo.</summary>
    internal static (GridLength Sidebar, GridLength Workspace, bool ShowSplitter) ResolveColumns(
        bool panoramica, bool collapsed, double sidebarWidth, bool focusZen)
    {
        if (focusZen) return (new GridLength(0), new GridLength(1, GridUnitType.Star), false);
        if (panoramica) return (new GridLength(1, GridUnitType.Star), new GridLength(0), false);
        if (collapsed) return (new GridLength(0), new GridLength(1, GridUnitType.Star), false);
        var width = sidebarWidth > 0 ? sidebarWidth : 280d;
        return (new GridLength(width), new GridLength(1, GridUnitType.Star), true);
    }

    // Un x:Name su una ColumnDefinition NON genera un campo (il generatore Avalonia cabla solo
    // gli elementi dell'albero visuale, e una ColumnDefinition non ne fa parte): si indicizzano.
    private ColumnDefinition SidebarColumn => HomeWorkspace.ColumnDefinitions[0];
    private ColumnDefinition WorkspaceColumn => HomeWorkspace.ColumnDefinitions[2];

    private void ApplyColumns()
    {
        var w = _viewModel.Workspace;
        var (sidebar, workspace, showSplitter) =
            ResolveColumns(w.IsPanoramicaActive, w.SidebarCollapsed, w.SidebarWidth, w.IsFocusZen);
        SidebarColumn.Width = sidebar;
        WorkspaceColumn.Width = workspace;
        SidebarSplitter.IsVisible = showSplitter;
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.IsPanoramicaActive)
            or nameof(WorkspaceViewModel.SidebarCollapsed)
            or nameof(WorkspaceViewModel.SidebarWidth)
            or nameof(WorkspaceViewModel.IsFocusZen)
            or nameof(WorkspaceViewModel.ActiveSubjectTab))
            ApplyColumns();
    }

    /// <summary>Il caricamento iniziale lanciato da <c>Opened</c> (async void: nessuno puo' attenderlo). I test lo
    /// attendono per non correre contro il ripristino dello stato del workspace.</summary>
    internal Task Initialization { get; private set; } = Task.CompletedTask;

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            ApplyChrome(IsExtendedIntoWindowDecorations);
            if (_viewModel is null) return;
            _viewModel.Workspace.PropertyChanged += OnWorkspacePropertyChanged;
            ApplyColumns();
            Initialization = _viewModel.InitializeAsync().AsTask();
            await Initialization.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Un guasto transitorio all'avvio non deve uscire come eccezione non osservata (CR15).
            Diagnostics.CrashLogger.Log("MainWindow.OnOpened", ex);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.Workspace.PropertyChanged -= OnWorkspacePropertyChanged;
    }

    /// <summary>D7. L'estensione dell'area client e' una RICHIESTA (XAML): su Windows la piattaforma
    /// la accoglie e la barra di 36px e' nostra (i ruoli le danno trascinamento, doppio clic e snap
    /// layout); su Linux/X11 la ignora — misurato — e restano le decorazioni di sistema, che
    /// spostano, massimizzano e ridimensionano da sole. In quel caso i nostri tre pulsanti di
    /// didascalia sarebbero doppioni: si nascondono. Si guarda il risultato, non il sistema.</summary>
    internal void ApplyChrome(bool extended)
    {
        MinButton.IsVisible = extended;
        MaxButton.IsVisible = extended;
        CloseButton.IsVisible = extended;
    }

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Il risultato dell'estensione non e' definitivo a Opened: la piattaforma puo' riportarlo (o
        // cambiarlo) dopo, e i pulsanti di didascalia devono seguirlo.
        if (change.Property == IsExtendedIntoWindowDecorationsProperty)
            ApplyChrome(change.GetNewValue<bool>());

        // Una finestra massimizzata con chrome nostro puo' sfondare l'area di lavoro del bordo di
        // ridimensionamento invisibile. WPF compensava con un margine 7 scritto a mano; qui il
        // numero lo dice la piattaforma, e lo puo' cambiare anche senza che cambi lo stato.
        if (change.Property == WindowStateProperty || change.Property == OffScreenMarginProperty)
            RootGrid.Margin = WindowState == WindowState.Maximized ? OffScreenMargin : default;

        if (change.Property != WindowStateProperty) return;
        var maximized = WindowState == WindowState.Maximized;
        MaxIcon.Data = GlyphFor(WindowState);
        MaxButton.SetValue(AutomationProperties.NameProperty,
            maximized ? Presentation.Resources.Strings.Window_Restore : Presentation.Resources.Strings.Window_Maximize);
    }

    private void OnWindowKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null) return;

        // Esc dentro una casella di testo e' della casella (annulla la rinomina): il gestore in tunnel lo vede per
        // primo e, se lo prendesse, la casella non lo riceverebbe mai e il nome scartato verrebbe salvato al
        // LostFocus (review F1, I3). Ctrl+1/2/3 restano globali, come in WPF.
        if (e.Key == Key.Escape && e.Source is TextBox) return;

        // Esc con una selezione multipla: la si annulla prima di ogni gestione di colonna.
        if (e.Key == Key.Escape && _viewModel.Home.HasMultiSelection)
        {
            _viewModel.Home.ClearSelection();
            e.Handled = true;
            return;
        }

        // Esc sullo schermo del tab attivo: esce dal focus se attivo, altrimenti chiude l'ultima
        // colonna aperta.
        if (e.Key == Key.Escape && _viewModel.Workspace.ActiveSubjectTab?.Screen is { } screen)
        {
            if (screen.IsFocusActive) screen.ExitFocusCommand.Execute(null);
            else screen.CloseLastColumn();
            e.Handled = true;
            return;
        }

        // Ctrl+1/2/3 scelgono una sotto-scheda della Home (via le KeyBindings dello XAML) E
        // riportano al workspace Home. Qui si cambia solo il workspace e si lascia e.Handled a
        // false, cosi' la KeyBinding scatta comunque.
        if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control) return;
        if (e.Key is Key.D1 or Key.D2 or Key.D3 or Key.NumPad1 or Key.NumPad2 or Key.NumPad3)
        {
            if (_viewModel.ShowHomeCommand.CanExecute(null))
                _viewModel.ShowHomeCommand.Execute(null);
        }
    }

    private async void OnSidebarSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        try
        {
            // Si legge la larghezza RESA: il drag ha scritto un valore locale sulla colonna e il
            // ViewModel non lo sa. Poi si ri-applica ResolveColumns, cosi' lo stato del ViewModel
            // torna a essere l'unica sorgente di verita' (senza, la panoramica non ri-espande piu'
            // la Home — e' la stessa regressione che in WPF costava il ri-aggancio dei binding).
            _viewModel.Workspace.SidebarWidth = Math.Clamp(SidebarColumn.ActualWidth, 0, 460);
            ApplyColumns();
            await _viewModel.Workspace.SaveStateAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Diagnostics.CrashLogger.Log("MainWindow.OnSidebarSplitterDragCompleted", ex);
        }
    }
}
