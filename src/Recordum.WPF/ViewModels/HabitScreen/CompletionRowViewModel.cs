using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionRowViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly Action<CompletionRowViewModel>? _onDeleted;

    public Completion Completion { get; }
    public IReadOnlyList<CompletionValue> Values { get; }

    public Guid Id => Completion.Id;
    public DateTime CompletedAt => Completion.CompletedAt;
    public bool IsRetroactive => Completion.IsRetroactive;
    public string? Notes => Completion.Notes;
    public string Display => CompletedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string? _editedNotes;
    [ObservableProperty] private DateTime _editedDate;

    public event EventHandler<Guid>? EditRequested;

    public CompletionRowViewModel(
        Completion completion,
        ICompletionService completions,
        IReadOnlyList<CompletionValue>? values = null,
        Action<CompletionRowViewModel>? onDeleted = null)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(completions);
        Completion = completion;
        _completions = completions;
        _onDeleted = onDeleted;
        Values = values ?? [];
        EditedNotes = completion.Notes;
        EditedDate = completion.CompletedAt;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditedNotes = Notes;
        EditedDate = CompletedAt;
        IsEditing = true;
        EditRequested?.Invoke(this, Id);
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveEditAsync(CancellationToken ct)
    {
        var newNotes = EditedNotes;
        var newDate = DateTime.SpecifyKind(EditedDate, DateTimeKind.Utc);
        var newMs = new DateTimeOffset(newDate, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await _completions.UpdateAsync(Id, c =>
        {
            c.Notes = newNotes;
            c.CompletedAt = newDate;
            c.CompletedAtMs = newMs;
        }, ct).ConfigureAwait(true);
        Completion.Notes = newNotes;
        Completion.CompletedAt = newDate;
        Completion.CompletedAtMs = newMs;
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(CompletedAt));
        OnPropertyChanged(nameof(Display));
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken ct)
    {
        await _completions.DeleteAsync(Id, ct).ConfigureAwait(true);
        _onDeleted?.Invoke(this);
    }
}
