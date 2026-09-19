using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class NotificationsSectionViewModel(INotificationService notifications) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Notifications;

    private readonly INotificationService _notifications = notifications;
    internal Guid NodeId { get; private set; }

    public ObservableCollection<NotificationRowViewModel> Notifications { get; } = new();

    public async Task LoadAsync(HabitNode node)
    {
        NodeId = node.Id;
        Notifications.Clear();
        foreach (var n in await _notifications.ListByHabitAsync(node.Id))
            Notifications.Add(new NotificationRowViewModel(n, _notifications, this));
    }

    [RelayCommand]
    private async Task AddNotificationAsync()
    {
        if (NodeId == Guid.Empty) return;
        var draft = new Notification
        {
            Id = Guid.NewGuid(),
            HabitNodeId = NodeId,
            NotifyType = NotifyType.Reminder,
            NotifyTime = new TimeOnly(9, 0),
            IsEnabled = true,
        };
        await ApplyAsync(() => _notifications.CreateAsync(draft).AsTask());
        Notifications.Add(new NotificationRowViewModel(draft, _notifications, this));
        RevalidateDuplicates();
    }

    [RelayCommand]
    private async Task RemoveNotificationAsync(NotificationRowViewModel row)
    {
        if (row is null) return;
        await ApplyAsync(() => _notifications.DeleteAsync(row.Id).AsTask());
        Notifications.Remove(row);
        RevalidateDuplicates();
    }

    internal void RevalidateDuplicates()
    {
        var duplicate = Notifications
            .GroupBy(n => (n.NotifyType, n.NotifyTime))
            .Any(g => g.Count() > 1);
        if (duplicate)
        {
            ErrorMessage = Strings.Editor_Error_NotificationDuplicate;
            Status = EditorStatus.Error;
        }
        else if (Status == EditorStatus.Error && ErrorMessage == Strings.Editor_Error_NotificationDuplicate)
        {
            ErrorMessage = null;
            Status = EditorStatus.Saved;
        }
    }
}

public sealed partial class NotificationRowViewModel(Notification model, INotificationService notifications, NotificationsSectionViewModel parent) : ObservableObject
{
    private readonly INotificationService _notifications = notifications;
    private readonly NotificationsSectionViewModel _parent = parent;
    public Guid Id { get; } = model.Id;

    [ObservableProperty] private NotifyType _notifyType = model.NotifyType;
    [ObservableProperty] private TimeOnly? _notifyTime = model.NotifyTime;
    [ObservableProperty] private bool _isEnabled = model.IsEnabled;

    partial void OnNotifyTypeChanged(NotifyType value)
    {
        _ = _notifications.UpdateAsync(Id, n => n.NotifyType = value).AsTask();
        _parent.RevalidateDuplicates();
    }

    partial void OnNotifyTimeChanged(TimeOnly? value)
    {
        _ = _notifications.UpdateAsync(Id, n => n.NotifyTime = value).AsTask();
        _parent.RevalidateDuplicates();
    }

    partial void OnIsEnabledChanged(bool value) =>
        _ = _notifications.UpdateAsync(Id, n => n.IsEnabled = value).AsTask();
}
