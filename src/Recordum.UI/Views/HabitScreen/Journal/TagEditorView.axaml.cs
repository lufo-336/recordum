using Avalonia.Controls;
using Avalonia.Input;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.UI.Views.HabitScreen.Journal;

public partial class TagEditorView : UserControl
{
    public TagEditorView()
    {
        InitializeComponent();
        TagBox.KeyDown += OnTagBoxKeyDown;
    }

    private void OnTagBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not TagEditorViewModel vm) return;
        e.Handled = true;
        var name = vm.Suggestions.Count > 0 ? vm.Suggestions[0] : vm.Query;
        vm.AddCommand.Execute(name);
    }
}
