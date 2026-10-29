using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.QuickAdd;

/// <summary>Le celle di registrazione dal tipo del ViewModel, costruendo SOLO quella view: il misurato e le serie in testa,
/// "+ a mano" della sessione. Gli altri casi hanno il loro comando (Head/HeadCommandTemplate).</summary>
public sealed class QuickAddTemplate : IDataTemplate
{
    public static QuickAddTemplate Instance { get; } = new();

    public bool Match(object? data) => data is QuickAddViewModelBase;

    public Control? Build(object? param) => param switch
    {
        MeasuredQuickAddViewModel => new MeasuredQuickAddView(),
        CounterSeriesQuickAddViewModel => new SeriesQuickAddView(),
        _ => null,
    };
}
