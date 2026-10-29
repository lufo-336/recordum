using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Converters;

namespace Recordum.UI.Views.HabitScreen;

public partial class HabitScreenView : UserControl
{
    private HabitScreenViewModel? _vm;

    public HabitScreenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
        ColumnResizeThumb.DragDelta += OnColumnResizeDelta;
        ColumnResizeThumb.DragCompleted += (_, _) => { if (_vm is { } vm) _ = vm.PersistColumnWidthsAsync(); };
        ColumnResizeThumb.DoubleTapped += OnColumnResizeReset;
        ActualThemeVariantChanged += (_, _) => ApplyAccent();   // la variante "testo" dipende dal tema
    }

    // ---- colonne: larghezza (pixel della Cronologia, resto alla Configura), MinWidth e maniglia dal ViewModel ----

    // Con due colonne aperte la Cronologia ha larghezza in pixel (quella del ViewModel) e la Configura prende il
    // resto: la maniglia sul confine sposta il confine esattamente del trascinamento.
    private void OnColumnResizeDelta(object? sender, VectorEventArgs e)
    {
        if (_vm is not { } vm) return;
        vm.SetColumnWidth(HabitColumn.Cronologia, vm.GetColumnWidth(HabitColumn.Cronologia) + e.Vector.X);
    }

    private void OnColumnResizeReset(object? sender, TappedEventArgs e)
    {
        if (_vm is not { } vm) return;
        vm.ResetColumnWidth(HabitColumn.Cronologia);
        _ = vm.PersistColumnWidthsAsync();
        e.Handled = true;
    }

    // Una colonna aperta non scende sotto 320; la griglia ha MinWidth = somma dei minimi (con due colonne la
    // Cronologia conta la sua larghezza in pixel): oltre, scorre lo ScrollViewer. Una chiusa non costruisce il
    // contenuto (decisione 3): il corpo della Cronologia si crea all'apertura e si butta alla chiusura.
    private void UpdateColumns()
    {
        if (_vm is not { } vm) return;
        // La Cronologia ha larghezza in pixel (e la maniglia) solo quando la Configura le sta accanto.
        var both = vm.ShowCronologia && vm.ShowConfigura && !vm.ShowNote;
        var defs = ColumnsGrid.ColumnDefinitions;
        // Colonne: 0 Diario (solo diari), 1 Cronologia, 2 Note, 3 Configura. Le altre si dividono il resto a stella.
        static GridLength Star(bool open) => open ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        defs[0].Width = Star(vm.ShowDiario);
        defs[0].MinWidth = vm.ShowDiario ? 320 : 0;
        defs[1].Width = both ? new GridLength(vm.CronologiaWidth) : Star(vm.ShowCronologia);
        defs[1].MinWidth = vm.ShowCronologia ? 320 : 0;
        defs[2].Width = Star(vm.ShowNote);
        defs[2].MinWidth = vm.ShowNote ? 320 : 0;
        defs[3].Width = Star(vm.ShowConfigura);
        defs[3].MinWidth = vm.ShowConfigura ? 320 : 0;
        ColumnsGrid.MinWidth = both ? vm.CronologiaWidth + 320 * (vm.ShowDiario ? 2 : 1) : vm.OpenColumnsMinWidth;
        ColumnResizeThumb.IsVisible = both;
        if (vm.ShowCronologia && CronologiaHost.Child is null)
            CronologiaHost.Child = new CronologiaColumnView { DataContext = vm };
        else if (!vm.ShowCronologia) CronologiaHost.Child = null;
        // Note: stessa regola; chiudendola, il testo non salvato si salva (mai perso).
        if (vm.ShowNote && NoteHost.Child is null)
            NoteHost.Child = new Notes.NotesColumnView { DataContext = vm.Notes };
        else if (!vm.ShowNote && NoteHost.Child is not null)
        {
            vm.Notes.SavePending();
            NoteHost.Child = null;
        }
        // Configura: stessa regola (decisione 3). Il suo DataContext e' l'editor, che vive piu' della colonna.
        if (vm.ShowConfigura && ConfiguraHost.Child is null)
            ConfiguraHost.Child = new HabitEditor.EditorPane { DataContext = vm.Editor };
        else if (!vm.ShowConfigura) ConfiguraHost.Child = null;
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        if (NoteHost.Child is not null) _vm?.Notes.SavePending();
        _vm = DataContext as HabitScreenViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        ApplyAccent();
        ConfiguraHost.Child = null;
        NoteHost.Child = null;
        CronologiaHost.Child = null;   // un altro ViewModel: il corpo si ricostruisce col nuovo contesto
        UpdateColumns();
    }

    // Il tab riusato per un'altra abitudine (anteprima) cambia Node senza ricostruire la view.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitScreenViewModel.Node)) ApplyAccent();
        if (e.PropertyName is nameof(HabitScreenViewModel.ShowCronologia) or nameof(HabitScreenViewModel.ShowConfigura)
            or nameof(HabitScreenViewModel.ShowDiario) or nameof(HabitScreenViewModel.ShowNote)
            or nameof(HabitScreenViewModel.CronologiaWidth) or nameof(HabitScreenViewModel.ConfiguraWidth)
            or nameof(HabitScreenViewModel.OpenColumnsMinWidth)) UpdateColumns();
    }

    // Le chiavi Fluent che dipingono l'accento dei controlli (check, slider, selezione di lista, fuoco dei
    // campi di testo). Misurato: la famiglia SystemAccentColor/Light1-3/Dark1-3 NON e' letta da nessuno di
    // questi controlli (i pennelli sono costruiti a livello tema e non rileggono una risorsa locale), quindi
    // non la si scrive; sono queste chiavi a contare. Ognuna ha il suo pixel in
    // HabitScreenViewTests.The_screen_accent_is_the_habit_colour.
    private static readonly string[] FluentAccentKeys =
    [
        "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
        "SystemControlHighlightListAccentLowBrush", "SystemControlHighlightListAccentMediumBrush",
        "SystemControlHighlightListAccentHighBrush", "TextControlBorderBrushFocused",
        "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver",
        "CheckBoxCheckBackgroundFillCheckedPressed",
        "CheckBoxCheckBackgroundStrokeCheckedPointerOver",
    ];

    private static readonly string[] HabitKeys = ["Brush.Habit", "Brush.HabitText", "Brush.HabitTint"];

    /// <summary>Le risorse non hanno DataContext: a ogni cambio di colore si riscrivono qui le chiavi Fluent. Senza
    /// colore valido si tolgono e torna l'accento dell'app. Ogni pennello eredita l'opacita' di quello del tema (la
    /// selezione di lista e' una tinta, non un blocco pieno). Brush.Accent non si tocca: resta l'inchiostro.</summary>
    private void ApplyAccent()
    {
        if (_vm?.Node?.Color is not { } hex || !Color.TryParse(hex, out var c))
        {
            foreach (var k in FluentAccentKeys.Concat(HabitKeys)) Resources.Remove(k);
            return;
        }
        var theme = ActualThemeVariant;
        // Il comando che registra (Button.record): il colore pieno, la sua variante "testo" e la tinta di riga.
        var tint = Application.Current!.TryGetResource("Opacity.RowTint", theme, out var t) && t is double d ? d : 0.09;
        Resources["Brush.Habit"] = new SolidColorBrush(c);
        Resources["Brush.HabitText"] = HabitTextBrushConverter.BrushFor(hex, theme);
        Resources["Brush.HabitTint"] = new SolidColorBrush(c, tint);
        foreach (var k in FluentAccentKeys)
        {
            var opacity = Application.Current!.TryGetResource(k, theme, out var v) && v is ISolidColorBrush b
                ? b.Opacity * b.Color.A / 255.0 : 1.0;
            Resources[k] = new SolidColorBrush(c, opacity);
        }
    }
}
