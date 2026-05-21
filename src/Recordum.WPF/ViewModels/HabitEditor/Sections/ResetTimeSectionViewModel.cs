using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class ResetTimeSectionViewModel(IHabitNodeService nodes) : SectionViewModelBase
{
    private readonly IHabitNodeService _nodes = nodes;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        _ = _nodes;
        return Task.CompletedTask;
    }
}
