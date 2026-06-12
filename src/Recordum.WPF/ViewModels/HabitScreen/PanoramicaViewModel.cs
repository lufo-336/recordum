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
    [NotifyPropertyChangedFor(nameof(TodayLabel))]
    private int _totalChildren;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TodayLabel))]
    private int _completedToday;

    /// <summary>True when the rollup has any row (habit OR subgroup) — drives the empty-state.</summary>
    public bool HasChildren => Children.Count > 0;

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
        Children.Clear();
        var habitCount = 0;
        var completedCount = 0;

        // Hierarchical rollup: subgroups appear as their own (indented) rows, habits beneath
        // them. The today counter still counts habits only.
        void Walk(Guid parentId, int indent)
        {
            foreach (var child in byParent[parentId])
            {
                if (child.HabitType is null)
                {
                    Children.Add(new PanoramicaChildRow(
                        child.HabitNodeId, child.Name, child.ColorHex, false,
                        child.CompletionsLast7Days, IsGroup: true, Indent: indent, ChildCount: child.ChildCount));
                    Walk(child.HabitNodeId, indent + 1);
                }
                else
                {
                    Children.Add(new PanoramicaChildRow(
                        child.HabitNodeId, child.Name, child.ColorHex, child.IsCompletedToday,
                        child.CompletionsLast7Days, Indent: indent));
                    habitCount++;
                    if (child.IsCompletedToday) completedCount++;
                }
            }
        }

        Walk(group.Id, 0);
        TotalChildren = habitCount;
        CompletedToday = completedCount;
        OnPropertyChanged(nameof(HasChildren));
        await Goal.LoadAsync(group, ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private void SelectChild(PanoramicaChildRow row) => ChildSelected?.Invoke(this, row.HabitNodeId);
}

/// <summary>One descendant row in the group overview: a habit, or a subgroup heading its
/// own indented sub-block.</summary>
public sealed record PanoramicaChildRow(
    Guid HabitNodeId,
    string Name,
    string ColorHex,
    bool IsCompletedToday,
    ImmutableArray<int> CompletionsLast7Days,
    bool IsGroup = false,
    int Indent = 0,
    int ChildCount = 0);
