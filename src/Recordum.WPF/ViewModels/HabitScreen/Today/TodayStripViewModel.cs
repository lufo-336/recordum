using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>
/// The always-visible per-type "Oggi" strip above the habit column workspace (spec §1). It picks a
/// per-type <see cref="ITodayContent"/> from the subject's <see cref="HabitType"/> so a habit is never
/// a blank landing even with every column closed. The content VMs reuse the existing quick-add /
/// detail / abstinence pieces rather than new engines.
/// </summary>
public sealed partial class TodayStripViewModel : ObservableObject, IDisposable
{
    private readonly IAbstinenceService _abstinence;
    private readonly ICompletionService _completions;
    private readonly IStreakAggregator _streaks;
    private readonly IHourlyDistributionQueryService _hourly;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IGoalService _goals;
    private readonly IClock _clock;

    /// <summary>The per-type content for the current subject, or <c>null</c> for a type with no strip
    /// (groups, or a type whose content has not been built yet).</summary>
    [ObservableProperty] private ITodayContent? _content;

    /// <summary>Bubbled from content that mutates state without a bus event (abstinence Start/Lapse) so
    /// the host can reload the columns reading the same state.</summary>
    public event EventHandler? Changed;

    /// <summary>Bubbled from content whose primary affordance navigates the workspace (Counter "+ Registra",
    /// Journal write/quick-note) so the host opens/focuses the matching column.</summary>
    public event EventHandler<TodayContentAction>? ActionRequested;

    public TodayStripViewModel(
        IAbstinenceService abstinence,
        ICompletionService completions,
        IStreakAggregator streaks,
        IHourlyDistributionQueryService hourly,
        IMetricDefinitionService metrics,
        IReferenceDataQueryService refData,
        IGoalService goals,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(abstinence);
        ArgumentNullException.ThrowIfNull(completions);
        ArgumentNullException.ThrowIfNull(streaks);
        ArgumentNullException.ThrowIfNull(hourly);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(refData);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(clock);
        _abstinence = abstinence;
        _completions = completions;
        _streaks = streaks;
        _hourly = hourly;
        _metrics = metrics;
        _refData = refData;
        _goals = goals;
        _clock = clock;
    }

    /// <summary>Builds and loads the per-type content for <paramref name="node"/>. Disposes any previous
    /// content first. Unknown/group types clear the strip.</summary>
    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        ITodayContent? next = node.HabitType switch
        {
            HabitType.Resistance => new ResistanceTodayContent(_abstinence, _clock),
            HabitType.Action => new ActionTodayContent(_completions, _streaks, _hourly, _clock),
            HabitType.Counter => new CounterTodayContent(_completions, _metrics, _refData, _goals, _clock),
            // State / Journal arms are added in Tasks 7–8.
            _ => null,
        };

        if (next is not null) await next.LoadAsync(node, ct).ConfigureAwait(true);
        SetContent(next);
    }

    private void SetContent(ITodayContent? next)
    {
        if (ReferenceEquals(Content, next)) return;
        DetachAndDispose(Content);
        if (next is ITodayContentChanges changes) changes.Changed += OnContentChanged;
        if (next is ITodayContentNavigates nav) nav.ActionRequested += OnContentAction;
        Content = next;
    }

    private void DetachAndDispose(ITodayContent? content)
    {
        if (content is ITodayContentChanges changes) changes.Changed -= OnContentChanged;
        if (content is ITodayContentNavigates nav) nav.ActionRequested -= OnContentAction;
        if (content is IDisposable d) d.Dispose();
    }

    private void OnContentChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void OnContentAction(object? sender, TodayContentAction action) => ActionRequested?.Invoke(this, action);

    public void Dispose() => DetachAndDispose(Content);
}
