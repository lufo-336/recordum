using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAssertions;
using Recordum.UI.Converters;
using Xunit;

namespace Recordum.UI.Tests.Converters;

/// <summary>Contro la risorsa VERA: l'<c>App</c> di TestAppBuilder è quella di produzione, quindi se
/// <c>Icons.axaml</c> non è mergiato in App.axaml questi test lo scoprono.</summary>
public class IconKeyToGeometryConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public sealed record IconHolder(string? Icon);

    [AvaloniaFact]
    public void A_known_key_returns_the_very_geometry_the_application_holds()
    {
        var app = Application.Current!;
        app.TryGetResource("Icon.Heart", app.ActualThemeVariant, out var expected).Should().BeTrue();

        var result = IconKeyToGeometryConverter.Instance.Convert("Icon.Heart", typeof(Geometry), null, Culture);

        result.Should().BeSameAs(expected);
        result.Should().BeOfType<StreamGeometry>();
    }

    [AvaloniaTheory]
    [InlineData("Icon.DoesNotExist")]
    [InlineData("")]
    [InlineData("Brush.Accent")]   // esiste ma non è una geometria
    [InlineData(null)]
    [InlineData(42)]
    public void Anything_else_returns_null_instead_of_throwing(object? input)
    {
        IconKeyToGeometryConverter.Instance.Convert(input, typeof(Geometry), null, Culture).Should().BeNull();
    }

    [AvaloniaFact]
    public void A_path_bound_through_the_converter_shows_the_icon_and_follows_the_key()
    {
        var path = new Path { Width = 24, Height = 24, Stretch = Stretch.Uniform, Stroke = Brushes.Black };
        path.Bind(Path.DataProperty, new Binding(nameof(IconHolder.Icon))
        {
            Converter = IconKeyToGeometryConverter.Instance,
        });
        var window = new Window { Content = path, DataContext = new IconHolder("Icon.Heart"), Width = 100, Height = 100 };
        window.Show();

        path.Data.Should().BeOfType<StreamGeometry>();
        var heart = path.Data;
        path.Bounds.Width.Should().BeGreaterThan(0, "il Path deve avere una superficie disegnabile");

        window.DataContext = new IconHolder("Icon.Moon");
        path.Data.Should().BeOfType<StreamGeometry>().And.NotBeSameAs(heart);

        window.DataContext = new IconHolder("Icon.DoesNotExist");
        path.Data.Should().BeNull("una chiave sconosciuta lascia il Path vuoto, non lo rompe");
    }

    [Fact]
    public void It_cannot_convert_back()
    {
        var act = () => IconKeyToGeometryConverter.Instance.ConvertBack(null, typeof(string), null, Culture);

        act.Should().Throw<NotSupportedException>();
    }
}
