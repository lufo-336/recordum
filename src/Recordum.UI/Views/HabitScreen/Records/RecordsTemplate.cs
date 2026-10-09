using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels.HabitScreen;

namespace Recordum.UI.Views.HabitScreen.Records;

/// <summary>Sceglie la lista delle registrazioni dal tipo del ViewModel (registrazioni o periodi d'astinenza) e costruisce SOLO quella.</summary>
public sealed class RecordsTemplate : IDataTemplate
{
    public static RecordsTemplate Instance { get; } = new();

    public bool Match(object? data) => data is CompletionListViewModel or AbstinencePeriodListViewModel;

    public Control? Build(object? param) => param switch
    {
        CompletionListViewModel => new CompletionListView(),
        AbstinencePeriodListViewModel => new AbstinencePeriodListView(),
        _ => null,
    };
}
