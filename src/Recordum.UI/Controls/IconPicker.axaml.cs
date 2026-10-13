using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.UI.Converters;

namespace Recordum.UI.Controls;

/// <summary>Porting di <c>Views/IconPicker.xaml(.cs)</c>. <see cref="Icon"/> è una chiave di
/// <c>Themes/Icons.axaml</c> ("Icon.Heart"), two-way, <c>null</c> = nessuna icona. Offre le sole icone
/// d'abitudine (<see cref="IconCatalog"/>), non i glifi dell'interfaccia.</summary>
public partial class IconPicker : UserControl
{
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<IconPicker, string?>(nameof(Icon), defaultBindingMode: BindingMode.TwoWay);

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private readonly Flyout _flyout = new() { Placement = PlacementMode.BottomEdgeAlignedLeft };
    private readonly WrapPanel _grid = new();
    private readonly TextBox _search = new() { Name = "SearchBox", Margin = new Thickness(0, 0, 0, 8), PlaceholderText = Strings.IconPicker_SearchPrompt };

    public IconPicker()
    {
        InitializeComponent();
        AutomationProperties.SetName(Trigger, Strings.HabitBuilder_Icon);
        ToolTip.SetTip(Trigger, Strings.HabitBuilder_Icon);
        AutomationProperties.SetName(_search, Strings.IconPicker_SearchPrompt);

        var none = new Button { Name = "NoIcon", Content = Strings.IconPicker_None, Margin = new Thickness(0, 0, 0, 6) };
        AutomationProperties.SetName(none, Strings.IconPicker_None);
        none.Click += (_, _) => Pick(null);

        _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        _flyout.Content = new StackPanel
        {
            Width = 240,
            Children =
            {
                _search,
                none,
                new ScrollViewer { MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _grid },
            },
        };
        _flyout.Opened += (_, _) => _search.Focus();
        Trigger.Flyout = _flyout;
        Fill();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) { Refresh(); Fill(); }
    }

    private void Refresh() => TriggerGlyph.Data = IconKeyToGeometryConverter.Instance.Convert(Icon, typeof(Geometry), null, System.Globalization.CultureInfo.InvariantCulture) as Geometry;

    private void Fill()
    {
        _grid.Children.Clear();
        foreach (var key in IconCatalog.Search(_search.Text))
        {
            var name = key.StartsWith("Icon.", StringComparison.Ordinal) ? key[5..] : key;
            var cell = new Button
            {
                Tag = "icon",
                Width = 32,
                Height = 32,
                Margin = new Thickness(3),
                Padding = new Thickness(5),
                BorderThickness = new Thickness(key == Icon ? 2 : 1),
                Background = Brushes.Transparent,
                Content = new Avalonia.Controls.Shapes.Path
                {
                    Stretch = Stretch.Uniform,
                    StrokeThickness = 1.5,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    Data = IconKeyToGeometryConverter.Instance.Convert(key, typeof(Geometry), null, System.Globalization.CultureInfo.InvariantCulture) as Geometry,
                },
            };
            cell.SetResourceReference(Button.BorderBrushProperty, key == Icon ? "Brush.Accent" : "Brush.Border");
            ((Avalonia.Controls.Shapes.Path)cell.Content).SetResourceReference(Avalonia.Controls.Shapes.Shape.StrokeProperty, "Brush.Text");
            if (key == Icon) cell.Classes.Add("selected");
            AutomationProperties.SetName(cell, name);
            ToolTip.SetTip(cell, name);
            cell.Click += (_, _) => Pick(key);
            _grid.Children.Add(cell);
        }
    }

    private void Pick(string? key)
    {
        Icon = key;
        _flyout.Hide();
        Trigger.Focus();
    }
}
