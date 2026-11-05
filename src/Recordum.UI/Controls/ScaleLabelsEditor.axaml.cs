using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.UI.Controls;

/// <summary>Le etichette di una scala come elenco di campi; il <c>DataContext</c> è un
/// <see cref="ScaleLabelsEditorViewModel"/>. Di norma il testo si conferma uscendo dal campo (in Configura ogni conferma è una
/// scrittura sul DB); <see cref="LiveUpdate"/> lo conferma a ogni tasto, per il creatore dove "Conferma" si abilita mentre si scrive.</summary>
public partial class ScaleLabelsEditor : UserControl
{
    public static readonly StyledProperty<bool> LiveUpdateProperty =
        AvaloniaProperty.Register<ScaleLabelsEditor, bool>(nameof(LiveUpdate));

    public bool LiveUpdate { get => GetValue(LiveUpdateProperty); set => SetValue(LiveUpdateProperty, value); }

    public ScaleLabelsEditor()
    {
        InitializeComponent();
        AddHandler(TextBox.TextChangedEvent, (_, e) =>
        {
            if (LiveUpdate && e.Source is TextBox { DataContext: ScaleLabelRowViewModel row } box && row.Text != (box.Text ?? string.Empty))
                row.Text = box.Text ?? string.Empty;
        });
    }
}
