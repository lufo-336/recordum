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
        // Il nome noto esatto (con le sue maiuscole), altrimenti quello digitato (si crea): mai il primo per prefisso.
        var typed = vm.Query.Trim();
        vm.AddCommand.Execute(vm.Suggestions.FirstOrDefault(n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase)) ?? typed);
    }
}
