using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionRowViewModel : ObservableObject
{
    public Completion Completion { get; }
    public IReadOnlyList<CompletionValue> Values { get; }

    public Guid Id => Completion.Id;
    public DateTime CompletedAt => Completion.CompletedAt;
    public bool IsRetroactive => Completion.IsRetroactive;
    public string? Notes => Completion.Notes;

    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? DeleteRequested;

    public CompletionRowViewModel(Completion completion, IReadOnlyList<CompletionValue>? values = null)
    {
        Completion = completion;
        Values = values ?? [];
    }

    [RelayCommand]
    private void Edit() => EditRequested?.Invoke(this, Id);

    [RelayCommand]
    private void Delete() => DeleteRequested?.Invoke(this, Id);
}
