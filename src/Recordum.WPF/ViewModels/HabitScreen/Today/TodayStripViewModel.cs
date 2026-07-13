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
    private readonly IClock _clock;

    /// <summary>The per-type content for the current subject, or <c>null</c> for a type with no strip
    /// (groups, or a type whose content has not been built yet).</summary>
    [ObservableProperty] private ITodayContent? _content;

    /// <summary>Bubbled from content that mutates state without a bus event (abstinence Start/Lapse) so
    /// the host can reload the columns reading the same state.</summary>
    public event EventHandler? Changed;

    public TodayStripViewModel(IAbstinenceService abstinence, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(abstinence);
        ArgumentNullException.ThrowIfNull(clock);
        _abstinence = abstinence;
        _clock = clock;
    }

    /// <summary>Builds and loads the per-type content for <paramref name="node"/>. Disposes any previous
    /// content first. Unknown/group types clear the strip.</summary>
    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        // CA1859: with only the Resistance arm the analyzer wants the concrete type; the best-common-type
        // widens back to ITodayContent once the other per-type arms land (Tasks 5–8). Remove then.
#pragma warning disable CA1859
        ITodayContent? next = node.HabitType switch
        {
            HabitType.Resistance => new ResistanceTodayContent(_abstinence, _clock),
            // Action / Counter / State / Journal arms are added in Tasks 5–8.
            _ => null,
        };
#pragma warning restore CA1859

        if (next is not null) await next.LoadAsync(node, ct).ConfigureAwait(true);
        SetContent(next);
    }

    private void SetContent(ITodayContent? next)
    {
        if (ReferenceEquals(Content, next)) return;
        DetachAndDispose(Content);
        if (next is ITodayContentChanges changes) changes.Changed += OnContentChanged;
        Content = next;
    }

    private void DetachAndDispose(ITodayContent? content)
    {
        if (content is ITodayContentChanges changes) changes.Changed -= OnContentChanged;
        if (content is IDisposable d) d.Dispose();
    }

    private void OnContentChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => DetachAndDispose(Content);
}
