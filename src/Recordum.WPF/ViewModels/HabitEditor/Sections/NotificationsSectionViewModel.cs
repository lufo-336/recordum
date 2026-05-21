using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class NotificationsSectionViewModel(INotificationService notifications) : SectionViewModelBase
{
    private readonly INotificationService _notifications = notifications;
    internal Guid NodeId { get; private set; }

    public Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        _ = _notifications;
        return Task.CompletedTask;
    }
}
