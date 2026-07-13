using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>
/// The always-visible per-type "Oggi" strip above the habit column workspace (spec §1). It picks a
/// per-type <see cref="ITodayContent"/> from the subject's <see cref="HabitType"/> so a habit is never
/// a blank landing even with every column closed. The content VMs (Tasks 4–8) reuse the existing
/// quick-add / detail / abstinence pieces rather than new engines.
/// </summary>
public sealed partial class TodayStripViewModel : ObservableObject, IDisposable
{
    /// <summary>The per-type content for the current subject, or <c>null</c> for a type with no strip
    /// (groups, or a type whose content has not been built yet).</summary>
    [ObservableProperty] private ITodayContent? _content;

    /// <summary>Builds and loads the per-type content for <paramref name="node"/>. Disposes any previous
    /// content first. Unknown/group types clear the strip.</summary>
    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        ITodayContent? next = node.HabitType switch
        {
            // Per-type content arms are added in Tasks 4–8.
            _ => null,
        };

        await LoadContentAsync(next, node, ct).ConfigureAwait(true);
        SetContent(next);
    }

    /// <summary>Runs a content VM's own async load (each arm added with its task); no-op for null.</summary>
    private static Task LoadContentAsync(ITodayContent? content, HabitNode node, CancellationToken ct)
        => Task.CompletedTask;

    private void SetContent(ITodayContent? next)
    {
        if (ReferenceEquals(Content, next)) return;
        DetachAndDispose(Content);
        Content = next;
    }

    private static void DetachAndDispose(ITodayContent? content)
    {
        if (content is IDisposable d) d.Dispose();
    }

    public void Dispose() => DetachAndDispose(Content);
}
