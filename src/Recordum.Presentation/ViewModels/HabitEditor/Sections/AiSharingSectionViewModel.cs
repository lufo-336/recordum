using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class AiSharingSectionViewModel(IHabitNodeService nodes) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_AiSharing;

    public override string Summary => Strings.Editor_Summary_ComingSoon;

    private readonly IHabitNodeService _nodes = nodes;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private AiDataSharing _aiDataSharing = AiDataSharing.None;

    public Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            AiDataSharing = node.AiDataSharing;
        }
        finally { _loading = false; }
        return Task.CompletedTask;
    }

    partial void OnAiDataSharingChanged(AiDataSharing value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _nodes.UpdateAsync(NodeId, n => n.AiDataSharing = value).AsTask());
    }
}
