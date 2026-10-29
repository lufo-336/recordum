using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.UI.Views.HabitScreen.Records;

public partial class CompletionListView : UserControl
{
    public CompletionListView()
    {
        InitializeComponent();
        // Il modello della riga dal tipo della lista. Chi ospita la vista la ricostruisce a ogni cambio d'abitudine
        // (CurrentList), quindi IsJournal è già quello giusto qui.
        DataContextChanged += (_, _) => RowsList.ItemTemplate =
            (IDataTemplate)Resources[DataContext is CompletionListViewModel { IsJournal: true } ? "JournalRow" : "PlainRow"]!;
    }
}
