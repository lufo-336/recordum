using Avalonia.Controls;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.Head;

public partial class ActionCommandView : UserControl
{
    public ActionCommandView()
    {
        InitializeComponent();
        RecordButton.Click += OnRecordClick;
    }

    // UX2A-9: IsChecked e' OneWay da IsDone, ma il ToggleButton si inverte da solo al clic. Se il comando non cambia nulla
    // (la guardia IsSaving, un errore) IsDone non notifica e il pulsante resterebbe diverso dallo stato. Il clic lo rimette
    // a IsDone; quando il salvataggio cambia IsDone, il binding lo porta dove deve stare.
    private void OnRecordClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ActionQuickAddViewModel vm) RecordButton.IsChecked = vm.IsDone;
    }
}
