using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.QuickAdd;

/// <summary>Sceglie il pannello di registrazione dal tipo del ViewModel e costruisce SOLO quello. Il diario non arriva qui (decisione 3 di F2a).</summary>
public sealed class QuickAddTemplate : IDataTemplate
{
    public static QuickAddTemplate Instance { get; } = new();

    public bool Match(object? data) => data is QuickAddViewModelBase;

    public Control? Build(object? param) => param switch
    {
        ActionQuickAddViewModel => new ActionQuickAddView(),
        CounterTapQuickAddViewModel => new CounterTapQuickAddView(),
        AbstinenceQuickAddViewModel => new AbstinenceQuickAddView(),
        MeasuredQuickAddViewModel => new MeasuredQuickAddView(),
        CounterSeriesQuickAddViewModel => new SeriesQuickAddView(),
        _ => null,
    };
}
