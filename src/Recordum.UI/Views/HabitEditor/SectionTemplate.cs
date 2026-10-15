using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Views.HabitEditor.Sections;

namespace Recordum.UI.Views.HabitEditor;

/// <summary>Costruisce, per una sezione, l'Expander (chiuso di default) col SOLO contenuto del suo tipo. Una sezione
/// senza ramo torna null: nessun Expander vuoto, nulla costruito. Le notifiche non hanno ramo e non ne avranno
/// finche' non arriva E14 (decisione 4); DataSourceSectionViewModel non e' in Sections e non ha view (R2).</summary>
public sealed class SectionTemplate : IDataTemplate
{
    public bool Match(object? data) => data is SectionViewModelBase;

    public Control? Build(object? param)
    {
        if (param is not SectionViewModelBase section || Content(section) is not { } content) return null;
        var expander = new Expander { Margin = new Avalonia.Thickness(0, 0, 0, 6), Content = content, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        expander.Bind(Expander.HeaderProperty, new Binding(nameof(SectionViewModelBase.Title)));
        expander.Bind(Expander.IsExpandedProperty, new Binding(nameof(SectionViewModelBase.IsExpanded)) { Mode = BindingMode.TwoWay });
        expander.Bind(Visual.IsVisibleProperty, new Binding(nameof(SectionViewModelBase.IsVisible)));
        return expander;
    }

    private static Control? Content(SectionViewModelBase section) => section switch
    {
        SchedulingSectionViewModel => new SchedulingSectionView(),
        StreakSectionViewModel => new StreakSectionView(),
        GoalSectionViewModel => new GoalSectionView(),
        ResetTimeSectionViewModel => new ResetTimeSectionView(),
        RegistrationSectionViewModel => new RegistrationSectionView(),
        HomeRowSectionViewModel => new HomeRowSectionView(),
        MetricsSectionViewModel => new MetricsSectionView(),
        ScalesSectionViewModel => new ScalesSectionView(),
        TimerSectionViewModel => new TimerSectionView(),
        TagsSectionViewModel => new TagsSectionView(),
        NotesSectionViewModel => new NotesSectionView(),
        AiSharingSectionViewModel => new AiSharingSectionView(),
        _ => null,
    };
}
