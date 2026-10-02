using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels;

namespace Recordum.UI.Views.Rows;

/// <summary>Sceglie la vista di riga dal tipo di nodo. In WPF era un DataTemplateSelector che
/// risolveva due chiavi di risorsa; qui è un IDataTemplate, e l'istanza condivisa permette al
/// template del gruppo di riferirsi a sé stesso (in XAML una StaticResource auto-referenziale
/// dentro GroupRowTemplate.xaml non si risolveva).</summary>
public sealed class HomeRowTemplate : IDataTemplate
{
    public static readonly HomeRowTemplate Instance = new();

    public bool Match(object? data) => data is HomeRowViewModel;

    public Control? Build(object? param) => param is HomeRowViewModel row
        ? row.IsGroup ? new GroupRow() : new HabitRow()
        : null;
}
