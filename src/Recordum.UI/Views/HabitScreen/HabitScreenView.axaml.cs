using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitScreen;

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
    }

    // Il tab riusato per un'altra abitudine (anteprima) cambia Node senza ricostruire la view.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitScreenViewModel.Node)) ApplyAccent();
    }

    /// <summary>Le risorse non hanno DataContext: a ogni cambio di colore si riscrivono qui. Brush.Accent
    /// per i nostri controlli; SystemAccentColor e le sei sfumature per quelli Fluent (check, slider,
    /// fuoco), che leggono quelle (App.axaml). Senza abitudine/colore valido si tolgono, e torna l'accento
    /// dell'app.</summary>
    private void ApplyAccent()
    {
        string[] keys = ["Brush.Accent", "SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorLight2",
            "SystemAccentColorLight3", "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
            "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
            "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundFillCheckedPressed", "CheckBoxCheckBackgroundStrokeChecked",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed"];
        if (_vm?.Node?.Color is not { } hex || !Color.TryParse(hex, out var c))
        {
            foreach (var k in keys) Resources.Remove(k);
            return;
        }
        Resources["Brush.Accent"] = new SolidColorBrush(c);
        Resources["SystemAccentColor"] = c;
        Resources["SystemAccentColorLight1"] = Mix(c, Colors.White, 0.2);
        Resources["SystemAccentColorLight2"] = Mix(c, Colors.White, 0.4);
        Resources["SystemAccentColorLight3"] = Mix(c, Colors.White, 0.6);
        Resources["SystemAccentColorDark1"] = Mix(c, Colors.Black, 0.2);
        Resources["SystemAccentColorDark2"] = Mix(c, Colors.Black, 0.4);
        Resources["SystemAccentColorDark3"] = Mix(c, Colors.Black, 0.6);
        // Le famiglie SystemAccentColor non bastano: i pennelli Fluent derivati (AccentFillColor*Brush, e
        // quelli del check) sono costruiti a livello tema e non rileggono la risorsa locale. Misurato: il
        // check checked restava #3B82F6. Si ridefiniscono qui i pennelli che il template legge.
        var brush = new SolidColorBrush(c);
        foreach (var k in keys.Skip(8)) Resources[k] = brush;
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
