using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAssertions;
using Recordum.UI.Converters;
using Recordum.UI.Theming;
using Xunit;

namespace Recordum.UI.Tests.Styles;

/// <summary>GIO-24: il numero del giorno sulla mappa era sempre nel colore del testo; su una cella piena di un colore
/// scuro (cremisi) in chiaro faceva 3,5:1. Ora è, fra il testo e il suo contrario, quello che si legge meglio sulla cella.</summary>
public class HeatLevelTextTests
{
    [AvaloniaTheory]
    [InlineData("#E11D48")]   // cremisi: il caso dei PNG
    [InlineData("#2F7FC1")]
    [InlineData("#EAB308")]   // giallo: qui vince il testo scuro anche sulla cella piena
    [InlineData("#111827")]
    public void The_day_number_reads_on_every_step_in_both_themes(string habit)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            for (var step = 1; step <= 4; step++)
            {
                object?[] input = [step, habit, theme];
                var fill = ((ISolidColorBrush)new HeatLevelBrushConverter().Convert(input, typeof(IBrush), null, null!)!).Color;
                var ink = ((ISolidColorBrush)new HeatLevelTextBrushConverter().Convert(input, typeof(IBrush), null, null!)!).Color;
                // La regola: il migliore dei due testi (quello del tema chiaro e quello dello scuro). Sui toni medi nessun
                // colore arriva a 4,5:1; il peggiore misurato qui è 3,7 (cremisi pieno in chiaro), prima era 2,1.
                foreach (var other in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    var candidate = HabitTextBrushConverter.Resource<ISolidColorBrush>(Avalonia.Application.Current!, "Brush.Text", other)!.Color;
                    HabitColors.Contrast(ink, fill).Should().BeGreaterThanOrEqualTo(HabitColors.Contrast(candidate, fill), $"{habit}, gradino {step}, {theme}");
                }
                HabitColors.Contrast(ink, fill).Should().BeGreaterThanOrEqualTo(3.7, $"{habit}, gradino {step}, {theme}");
            }
    }

    [AvaloniaFact]
    public void An_empty_cell_keeps_the_style_colour()
    {
        object?[] input = [0, "#E11D48", ThemeVariant.Light];
        new HeatLevelTextBrushConverter().Convert(input, typeof(IBrush), null, null!)
            .Should().Be(Avalonia.AvaloniaProperty.UnsetValue, "fuori mese e ricaduta hanno il loro colore dallo stile");
    }
}
