using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Views.Rows;

/// <summary>Sceglie la vista di riga dal tipo di nodo. In WPF era un DataTemplateSelector che
/// risolveva due chiavi di risorsa; qui è un IDataTemplate con un'istanza condivisa. Le righe sono
/// una lista piatta (F1-PERF1): un gruppo non contiene più i suoi figli.</summary>
public sealed class HomeRowTemplate : IDataTemplate
{
    public static readonly HomeRowTemplate Instance = new();

    public bool Match(object? data) => data is HomeRowViewModel;

    public Control? Build(object? param) => param is HomeRowViewModel row
        ? row.IsGroup ? new GroupRow() : new HabitRow()
        : null;
}
