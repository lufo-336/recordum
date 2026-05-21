using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class StreakSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    private readonly IHabitConfigService _configs = configs;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        NodeId = node.Id;
        _ = cfg; _ = _configs;
        return Task.CompletedTask;
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;
}
