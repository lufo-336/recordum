using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Recordum.UI.Views.HabitEditor.Sections;

namespace Recordum.UI.Views.HabitEditor;

/// <summary>Quale parte di una sezione mostra una <see cref="ConfigRow"/>: il contenuto principale o quello "Avanzate".</summary>
public enum SectionPart { Main, Advanced }

/// <summary>La mappa tipo di sezione → view, per parte. Una sezione senza ramo torna null: nessuna riga, nulla costruito. Le
/// notifiche non hanno ramo finche' non arriva E14 (decisione 4); DataSourceSectionViewModel non e' in Sections e non ha view
/// (R2). Solo Serie, Obiettivo e Timer hanno una parte avanzata (<see cref="SectionViewModelBase.HasAdvanced"/>).</summary>
public static class SectionContent
{
    public static bool Has(SectionViewModelBase section, SectionPart part) => Factory(section, part) is not null;

    public static Control? Build(SectionViewModelBase section, SectionPart part) => Factory(section, part)?.Invoke();

    private static Func<Control>? Factory(SectionViewModelBase section, SectionPart part) => part == SectionPart.Advanced
        ? section switch
        {
            StreakSectionViewModel => () => new StreakAdvancedView(),
            GoalSectionViewModel => () => new GoalAdvancedView(),
            TimerSectionViewModel => () => new TimerAdvancedView(),
            _ => null,
        }
        : section switch
        {
            SchedulingSectionViewModel => () => new SchedulingSectionView(),
            StreakSectionViewModel => () => new StreakSectionView(),
            GoalSectionViewModel => () => new GoalSectionView(),
            ResetTimeSectionViewModel => () => new ResetTimeSectionView(),
            RegistrationSectionViewModel => () => new RegistrationSectionView(),
            HomeRowSectionViewModel => () => new HomeRowSectionView(),
            MetricsSectionViewModel => () => new MetricsSectionView(),
            ScalesSectionViewModel => () => new ScalesSectionView(),
            TimerSectionViewModel => () => new TimerSectionView(),
            TagsSectionViewModel => () => new TagsSectionView(),
            NotesSectionViewModel => () => new NotesSectionView(),
            AiSharingSectionViewModel => () => new AiSharingSectionView(),
            _ => null,
        };
}
