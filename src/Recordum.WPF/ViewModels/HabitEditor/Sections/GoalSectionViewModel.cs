using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class GoalSectionViewModel(IGoalService goals) : SectionViewModelBase
{
    private readonly IGoalService _goals = goals;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        _ = _goals;
        return Task.CompletedTask;
    }
}
