using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.UI.Views.HabitScreen.Journal;

/// <summary>La colonna Diario: il diario della sera o la nota veloce, dalla modalita' del composer, e SOLO quella
/// (CLAUDE.md 8). Non ricicla: un altro composer (un'altra abitudine) costruisce la sua view.</summary>
public sealed class JournalComposerTemplate : IDataTemplate
{
    public bool Match(object? data) => data is JournalQuickEntryViewModel;

    public Control? Build(object? param) => param switch
    {
        JournalQuickEntryViewModel { IsDiaryMode: true } => new JournalDiaryView(),
        JournalQuickEntryViewModel => new JournalQuickEntryView(),
        _ => null,
    };
}
