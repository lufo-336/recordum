using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAssertions;
using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>Prove strutturali per il Task 2: l'accento di sistema segue Brush.Accent (non il blu di
/// Windows), il Separator dei menu è esplicitamente Brush.Border, ListBoxItem ha l'angolo che
/// Recordum vuole. Non sono screenshot: leggono i valori risolti, come i test dei convertitori.</summary>
public class GlobalStylesTests
{
    [AvaloniaTheory]
    [InlineData("Light", "#FF1F1F1D")]
    [InlineData("Dark", "#FFE6E5E1")]
    public void The_system_accent_follows_Brush_Accent_not_Windows_blue(string variant, string expectedHex)
    {
        var app = Application.Current!;
        var v = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        app.TryGetResource("SystemAccentColor", v, out var accent);
        app.TryGetResource("Brush.Accent", v, out var token);

        accent.Should().Be(Color.Parse(expectedHex));
        ((ISolidColorBrush)token!).Color.Should().Be(Color.Parse(expectedHex),
            "SystemAccentColor e Brush.Accent devono essere LO STESSO inchiostro, non due colori simili per caso");
    }

    [AvaloniaFact]
    public void A_checked_CheckBox_fills_with_the_Recordum_accent_not_a_default_blue()
    {
        var cb = new CheckBox { IsChecked = true, Content = "x" };
        var window = new Window { Content = cb, Width = 100, Height = 60, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var found = Application.Current!.TryGetResource("CheckBoxCheckBackgroundFillChecked", ThemeVariant.Light, out var fill);
        found.Should().BeTrue("la chiave deve esistere nel tema Fluent");
        ((ISolidColorBrush)fill!).Color.Should().Be(Color.Parse("#FF1F1F1D"));   // l'inchiostro (direzione visiva §4.2)
    }

    [AvaloniaFact]
    public void Menu_separators_are_explicitly_Brush_Border()
    {
        var separator = new Separator();
        var window = new Window { Content = separator, Width = 100, Height = 20 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Application.Current!.TryGetResource("Brush.Border", window.ActualThemeVariant, out var expected);
        ((ISolidColorBrush)separator.Background!).Color.Should().Be(((ISolidColorBrush)expected!).Color);
    }

    [AvaloniaFact]
    public void ListBoxItem_has_Recordums_5px_corner_radius()
    {
        var list = new ListBox { ItemsSource = new[] { "a", "b" } };
        var window = new Window { Content = list, Width = 100, Height = 100 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var item = (ListBoxItem)list.ContainerFromIndex(0)!;
        item.CornerRadius.Should().Be(new CornerRadius(5));
    }
}
