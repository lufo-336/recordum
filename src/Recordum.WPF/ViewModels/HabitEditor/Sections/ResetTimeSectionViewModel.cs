using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

public sealed partial class ResetTimeSectionViewModel(IHabitNodeService nodes) : SectionViewModelBase
{
    private readonly IHabitNodeService _nodes = nodes;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private string _resetTimeText = string.Empty;

    public Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            ResetTimeText = node.ResetTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        }
        finally { _loading = false; }
        return Task.CompletedTask;
    }

    partial void OnResetTimeTextChanged(string value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = ApplyAsync(() => _nodes.UpdateAsync(NodeId, n => n.ResetTime = null).AsTask());
            return;
        }
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            ErrorMessage = "Reset time must be HH:mm (24h)";
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _nodes.UpdateAsync(NodeId, n => n.ResetTime = parsed).AsTask());
    }
}
