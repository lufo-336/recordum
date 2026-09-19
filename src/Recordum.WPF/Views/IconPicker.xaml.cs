using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Recordum.Presentation.Services;

namespace Recordum.WPF.Views;

/// <summary>
/// Bound to <see cref="Icon"/> — a resource key into <c>Resources/Icons.xaml</c>.
/// The trigger shows the selected icon; clicking it opens a popup with a searchable grid of the
/// curated Lucide set (<see cref="IconCatalog"/>). Picking an icon sets <see cref="Icon"/>.
/// </summary>
public partial class IconPicker : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(string),
        typeof(IconPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIconChanged));

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty SelectedIconGeometryProperty = DependencyProperty.Register(
        nameof(SelectedIconGeometry),
        typeof(Geometry),
        typeof(IconPicker),
        new PropertyMetadata(null));

    public Geometry? SelectedIconGeometry
    {
        get => (Geometry?)GetValue(SelectedIconGeometryProperty);
        private set => SetValue(SelectedIconGeometryProperty, value);
    }

    /// <summary>Currently displayed (search-filtered) icon resource keys.</summary>
    public ObservableCollection<string> Results { get; } = new();

    public IconPicker()
    {
        InitializeComponent();
        Reset(IconCatalog.All);
    }

    private void Reset(IReadOnlyList<string> keys)
    {
        Results.Clear();
        foreach (var k in keys) Results.Add(k);
    }

    private void OnTriggerClick(object sender, MouseButtonEventArgs e)
    {
        PickerPopup.IsOpen = !PickerPopup.IsOpen;
        e.Handled = true;
    }

    private void OnTriggerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            PickerPopup.IsOpen = !PickerPopup.IsOpen;
            e.Handled = true;
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) Reset(IconCatalog.Search(tb.Text));
    }

    private void OnIconClick(object sender, MouseButtonEventArgs e)
    {
        PickFrom(sender);
        e.Handled = true;
    }

    private void OnIconKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            PickFrom(sender);
            e.Handled = true;
        }
    }

    private void PickFrom(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is string key)
        {
            Icon = key;
            PickerPopup.IsOpen = false;
            Trigger.Focus();
        }
    }

    // Move keyboard focus to the search box when the popup opens, so keyboard users can type to
    // filter then Tab into the grid (Enter/Space picks, Escape closes).
    private void OnPopupOpened(object? sender, System.EventArgs e)
        => Dispatcher.BeginInvoke(
            new System.Action(() => SearchBox.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

    private void OnPopupKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            PickerPopup.IsOpen = false;
            Trigger.Focus();
            e.Handled = true;
        }
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (IconPicker)d;
        if (e.NewValue is not string key || string.IsNullOrWhiteSpace(key))
        {
            picker.SelectedIconGeometry = null;
            return;
        }
        picker.SelectedIconGeometry = Application.Current?.TryFindResource(key) as Geometry;
    }
}
