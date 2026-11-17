using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Recordum.UI.Behaviors;
using Recordum.Presentation.ViewModels.Scales;

namespace Recordum.UI.Controls;

/// <summary>L'editor di una scala; il <c>DataContext</c> è un <see cref="ScaleEditorViewModel"/>. Di norma i campi si
/// confermano uscendo (o con Invio): in Configura ogni conferma valida è una scrittura sul DB. <see cref="LiveUpdate"/>
/// li conferma a ogni tasto, per il creatore dove "Conferma" si abilita mentre si scrive.</summary>
public partial class ScaleEditor : UserControl
{
    private const double NarrowBelow = 480;

    public static readonly StyledProperty<bool> LiveUpdateProperty =
        AvaloniaProperty.Register<ScaleEditor, bool>(nameof(LiveUpdate));

    public bool LiveUpdate { get => GetValue(LiveUpdateProperty); set => SetValue(LiveUpdateProperty, value); }

    public ScaleEditor()
    {
        InitializeComponent();
        AddHandler(TextBox.TextChangedEvent, (_, e) =>
        {
            if (LiveUpdate && e.Source is TextBox box) Push(box);
        });
        CommitOnEnter.Watch(this);
        SizeChanged += (_, e) => Classes.Set("narrow", e.NewSize.Width < NarrowBelow);
    }

    private void Push(TextBox box)
    {
        if (DataContext is not ScaleEditorViewModel vm) return;
        var text = box.Text ?? string.Empty;
        switch (box.Name)
        {
            case "NameBox" when vm.Name != text: vm.Name = text; break;
            case "MinBox" when Int(text) is { } min && vm.MinValue != min: vm.MinValue = min; break;
            case "MaxBox" when Int(text) is { } max && vm.MaxValue != max: vm.MaxValue = max; break;
            case "LabelBox" when box.DataContext is ScaleAnchorRowViewModel r && r.Label != text: r.Label = text; break;
            case "ValueBox" when box.DataContext is ScaleAnchorRowViewModel r && Int(text) is { } v && r.Value != v: r.Value = v; break;
        }
    }

    private static int? Int(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign,
            CultureInfo.CurrentCulture, out var n) ? n : null;
}
