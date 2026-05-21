using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class TagsSectionViewModel(ITagService tags) : SectionViewModelBase
{
    private readonly ITagService _tags = tags;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        _ = _tags;
        return Task.CompletedTask;
    }
}
