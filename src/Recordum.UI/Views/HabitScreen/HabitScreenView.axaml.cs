using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Views.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen;

public partial class HabitScreenView : UserControl
{
    private HabitScreenViewModel? _vm;

    public HabitScreenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as HabitScreenViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        ApplyAccent();
        RebuildQuickAdd();
    }

    // Il tab riusato per un'altra abitudine (anteprima) cambia Node senza ricostruire la view.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitScreenViewModel.Node)) ApplyAccent();
        if (e.PropertyName == nameof(HabitScreenViewModel.QuickAddForCronologia)) RebuildQuickAdd();
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
