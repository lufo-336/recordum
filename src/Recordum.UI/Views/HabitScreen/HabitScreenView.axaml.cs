using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Recordum.UI.Views.HabitScreen.Records;

namespace Recordum.UI.Views.HabitScreen;

public partial class HabitScreenView : UserControl
{
    private HabitScreenViewModel? _vm;
    private HabitRecordsViewModel? _records;

    public HabitScreenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
        foreach (var thumb in new[] { CronologiaResizeThumb, ConfiguraResizeThumb })
        {
            thumb.DragDelta += OnColumnResizeDelta;
            thumb.DragCompleted += (_, _) => { if (_vm is { } vm) _ = vm.PersistColumnWidthsAsync(); };
            thumb.DoubleTapped += OnColumnResizeReset;
        }
    }

    // ---- colonne: larghezza (peso star), MinWidth e maniglie dal ViewModel ----

    private static HabitColumn ColumnOf(object? sender) => (HabitColumn)((Thumb)sender!).Tag!;

    // ponytail: il peso star e' la larghezza salvata, quindi un trascinamento di N px sposta la colonna di
    // circa N px solo se le altre non cambiano; una vera larghezza in pixel arriva se la proporzione stona.
    private void OnColumnResizeDelta(object? sender, VectorEventArgs e)
    {
        if (_vm is not { } vm) return;
        var column = ColumnOf(sender);
        vm.SetColumnWidth(column, vm.GetColumnWidth(column) + e.Vector.X);
    }

    private void OnColumnResizeReset(object? sender, TappedEventArgs e)
    {
        if (_vm is not { } vm) return;
        vm.ResetColumnWidth(ColumnOf(sender));
        _ = vm.PersistColumnWidthsAsync();
        e.Handled = true;
    }

    // Una colonna chiusa pesa 0 e non ha minimo; una aperta pesa quanto la sua larghezza e non scende sotto
    // OpenColumnsMinWidth/aperte (320). Il MinWidth della griglia e' la somma: oltre, scorre lo ScrollViewer.
    private void UpdateColumns()
    {
        if (_vm is not { } vm) return;
        SetColumn(0, vm.ShowCronologia, vm.CronologiaWidth);
        SetColumn(1, vm.ShowConfigura, vm.ConfiguraWidth);
        ColumnsGrid.MinWidth = vm.OpenColumnsMinWidth;
        var several = vm.ShowCronologia && vm.ShowConfigura;
        CronologiaResizeThumb.IsVisible = ConfiguraResizeThumb.IsVisible = several;
    }

    private void SetColumn(int index, bool open, double width)
    {
        var def = ColumnsGrid.ColumnDefinitions[index];
        def.Width = open ? new GridLength(width, GridUnitType.Star) : new GridLength(0);
        def.MinWidth = open ? 320 : 0;
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as HabitScreenViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        if (_records is not null) _records.PropertyChanged -= OnRecordsPropertyChanged;
        _records = _vm?.Records;
        if (_records is not null) _records.PropertyChanged += OnRecordsPropertyChanged;
        ApplyAccent();
        RebuildQuickAdd();
        RebuildRecords();
        UpdateColumns();
    }

    private void OnRecordsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitRecordsViewModel.CurrentList)) RebuildRecords();
    }

    // Come QuickAddHost: la lista cambia tipo con l'abitudine (registrazioni / periodi), quindi si ricostruisce
    // dal tipo; ricostruire a ogni SetContext anche a tipo invariato e' innocuo (la lista e' la stessa istanza).
    private void RebuildRecords()
    {
        var content = _records?.CurrentList;
        if (RecordsHost.Child?.DataContext == content && content is not null) return;
        var view = RecordsTemplate.Instance.Build(content);
        if (view is not null) view.DataContext = content;
        RecordsHost.Child = view;
    }

    // Il tab riusato per un'altra abitudine (anteprima) cambia Node senza ricostruire la view.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitScreenViewModel.Node)) ApplyAccent();
        if (e.PropertyName == nameof(HabitScreenViewModel.QuickAddForCronologia)) RebuildQuickAdd();
        if (e.PropertyName is nameof(HabitScreenViewModel.ShowCronologia) or nameof(HabitScreenViewModel.ShowConfigura)
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

    // Niente ContentControl: con un ContentTemplate statico il template si materializza anche a Content null.
    private void RebuildQuickAdd()
    {
        var content = _vm?.QuickAddForCronologia;
        var view = QuickAddTemplate.Instance.Build(content);
        if (view is not null) view.DataContext = content;
        QuickAddHost.Child = view;
    }

    /// <summary>Le risorse non hanno DataContext: a ogni cambio di colore si riscrivono qui. Brush.Accent
    /// per i nostri controlli, le chiavi Fluent per gli altri. Senza colore valido si tolgono e torna
    /// l'accento dell'app. Ogni pennello eredita l'opacita' di quello del tema (la selezione di lista e'
    /// una tinta, non un blocco pieno).</summary>
    private void ApplyAccent()
    {
        if (_vm?.Node?.Color is not { } hex || !Color.TryParse(hex, out var c))
        {
            Resources.Remove("Brush.Accent");
            foreach (var k in FluentAccentKeys) Resources.Remove(k);
            return;
        }
        Resources["Brush.Accent"] = new SolidColorBrush(c);
        var theme = ActualThemeVariant;
        foreach (var k in FluentAccentKeys)
        {
            var opacity = Application.Current!.TryGetResource(k, theme, out var v) && v is ISolidColorBrush b
                ? b.Opacity * b.Color.A / 255.0 : 1.0;
            Resources[k] = new SolidColorBrush(c, opacity);
        }
    }
}
