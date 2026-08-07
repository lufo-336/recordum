using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionRowViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly Action<CompletionRowViewModel>? _onDeleted;
    private readonly string? _overrideNotes;
    private readonly Func<Guid, CancellationToken, Task>? _deleteOverride;

    public Completion Completion { get; }
    public IReadOnlyList<CompletionValue> Values { get; }

    /// <summary>Pre-formatted "metric: value unit" / "metric: scale-label" lines for this completion's fields.</summary>
    public IReadOnlyList<string> FieldDisplays { get; }
    public bool HasFieldDisplays => FieldDisplays.Count > 0;

    /// <summary>False for journal rows: the diary text is edited in the Note column, not inline here.</summary>
    public bool CanInlineEdit { get; }

    public Guid Id => Completion.Id;
    public DateTime CompletedAt => Completion.CompletedAt;
    public bool IsRetroactive => Completion.IsRetroactive;
    public string? Notes => _overrideNotes ?? Completion.Notes;
    public string Display => CompletedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string? _editedNotes;
    [ObservableProperty] private DateTime _editedDate;

    public CompletionRowViewModel(
        Completion completion,
        ICompletionService completions,
        IReadOnlyList<CompletionValue>? values = null,
        Action<CompletionRowViewModel>? onDeleted = null,
        IReadOnlyList<string>? fieldDisplays = null,
        string? overrideNotes = null,
        Func<Guid, CancellationToken, Task>? deleteOverride = null,
        bool canInlineEdit = true)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(completions);
        Completion = completion;
        _completions = completions;
        _onDeleted = onDeleted;
        Values = values ?? [];
        FieldDisplays = fieldDisplays ?? [];
        _overrideNotes = overrideNotes;
        _deleteOverride = deleteOverride;
        CanInlineEdit = canInlineEdit;
        EditedNotes = completion.Notes;
        EditedDate = completion.CompletedAt;
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditedNotes = Notes;
        EditedDate = CompletedAt;
        IsEditing = true;
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
        if (_deleteOverride is not null)
            await _deleteOverride(Id, ct).ConfigureAwait(true);
        else
            await _completions.DeleteAsync(Id, ct).ConfigureAwait(true);
        _onDeleted?.Invoke(this);
    }
}
