using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;

namespace Recordum.UI.Controls;

/// <summary>La riga della Home di un <see cref="HomeRowPreviewModel"/>, tinta come in Home. Null = niente da mostrare.</summary>
public partial class RowPreview : UserControl
{
    public static readonly StyledProperty<HomeRowPreviewModel?> ModelProperty =
        AvaloniaProperty.Register<RowPreview, HomeRowPreviewModel?>(nameof(Model));

    /// <summary>Il tema in cui disegnarla; null = quello corrente. Non si chiama <c>Theme</c>: quel nome e' gia'
    /// <see cref="StyledElement.Theme"/> (il ControlTheme), e nasconderlo confonderebbe lo XAML.</summary>
    public static readonly StyledProperty<ThemeVariant?> VariantProperty =
        AvaloniaProperty.Register<RowPreview, ThemeVariant?>(nameof(Variant));

    public HomeRowPreviewModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public ThemeVariant? Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public RowPreview()
    {
        InitializeComponent();
        Card.DataContext = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty)
        {
            Card.DataContext = Model;
            Card.IsVisible = Model is not null;
        }
        else if (change.Property == VariantProperty) Scope.RequestedThemeVariant = Variant ?? ThemeVariant.Default;
    }
}
