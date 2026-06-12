using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.Detail;

namespace Recordum.WPF.ViewModels.HabitScreen;

/// <summary>Group overview column: today rollup, descendant habit rows, group goal (spec §A.2).</summary>
public sealed partial class PanoramicaViewModel : ObservableObject
{
    private readonly IHabitTreeQueryService _tree;

    public GoalProgressCardViewModel Goal { get; }
    public ObservableCollection<PanoramicaChildRow> Children { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChildren))]
    [NotifyPropertyChangedFor(nameof(TodayLabel))]
    private int _totalChildren;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodayLabel))]
    private int _completedToday;

    public bool HasChildren => TotalChildren > 0;

#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
    public string TodayLabel => string.Format(
        System.Globalization.CultureInfo.CurrentCulture,
        Resources.Strings.HabitScreen_Panoramica_Today, CompletedToday, TotalChildren);
#pragma warning restore CA1863

    /// <summary>Click on a child row: the host selects that habit in the Home tree (spec §A.3).</summary>
    public event EventHandler<Guid>? ChildSelected;

    public PanoramicaViewModel(IHabitTreeQueryService tree, IGoalService goals, ICompletionService completions, IClock clock)
    {
        _tree = tree;
        Goal = new GoalProgressCardViewModel(goals, completions, clock);
    }

    public async Task LoadAsync(HabitNode group, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        var rows = await _tree.GetHomeTreeAsync(ct).ConfigureAwait(true);
        var byParent = rows.ToLookup(r => r.ParentId);
        var habits = Descendants(group.Id, byParent).Where(r => r.HabitType is not null).ToList();
        Children.Clear();
        foreach (var h in habits)
            Children.Add(new PanoramicaChildRow(h.HabitNodeId, h.Name, h.ColorHex, h.IsCompletedToday, h.CompletionsLast7Days));
        TotalChildren = habits.Count;
        CompletedToday = habits.Count(h => h.IsCompletedToday);
        await Goal.LoadAsync(group, ct).ConfigureAwait(true);
    }

    private static IEnumerable<HomeTreeRow> Descendants(Guid parentId, ILookup<Guid?, HomeTreeRow> byParent)
    {
        foreach (var child in byParent[parentId])
        {
            yield return child;
            foreach (var nested in Descendants(child.HabitNodeId, byParent))
                yield return nested;
        }
    }

    [RelayCommand]
    private void SelectChild(PanoramicaChildRow row) => ChildSelected?.Invoke(this, row.HabitNodeId);
}

/// <summary>One descendant habit in the group overview.</summary>
public sealed record PanoramicaChildRow(Guid HabitNodeId, string Name, string ColorHex, bool IsCompletedToday, ImmutableArray<int> CompletionsLast7Days);
