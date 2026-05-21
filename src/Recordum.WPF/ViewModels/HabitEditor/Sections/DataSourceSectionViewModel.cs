using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class DataSourceSectionViewModel(IAppSettingService settings) : SectionViewModelBase
{
    private readonly IAppSettingService _settings = settings;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        NodeId = node.Id;
        _ = cfg; _ = _settings;
        return Task.CompletedTask;
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) =>
        node == NodeType.Habit && habit is HabitType.Action or HabitType.Counter or HabitType.Resistance or HabitType.State;
}
