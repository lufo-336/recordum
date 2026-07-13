using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>A per-type "Oggi" strip content ViewModel (one implementation per
/// <see cref="Recordum.Core.Enums.HabitType"/>). Built by <see cref="TodayStripViewModel"/> from the
/// subject's type and rendered by the matching DataTemplate in <c>TodayContentTemplates.xaml</c>.</summary>
public interface ITodayContent
{
    /// <summary>Populates the content for <paramref name="node"/>. Called once per subject load and on
    /// the host's reload paths.</summary>
    Task LoadAsync(HabitNode node, CancellationToken ct = default);
}

/// <summary>Optional companion interface for content that mutates state without a bus event (abstinence
/// Start/Lapse write no completions). The host reloads the columns reading the same state when this fires.</summary>
public interface ITodayContentChanges
{
    event EventHandler? Changed;
}
