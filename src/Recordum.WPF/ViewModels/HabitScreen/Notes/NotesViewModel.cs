using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.WPF.Bootstrap;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

public sealed partial class NotesViewModel : ObservableObject, IDisposable
{
    private readonly INoteService _notes;
    private readonly IClock _clock;
    private readonly List<IDisposable> _subs = [];
    private Guid _habitNodeId;

    public ObservableCollection<NoteRowViewModel> Notes { get; } = [];

    [ObservableProperty] private NoteRowViewModel? _selectedNote;
    [ObservableProperty] private string _editorMarkdown = string.Empty;

    public IWebView2Availability Availability { get; }

    public NotesViewModel(INoteService notes, IEventBus events, IClock clock, IWebView2Availability availability)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(availability);
        _notes = notes;
        _clock = clock;
        Availability = availability;
        _ = events; // subscribed in Task 21
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        _habitNodeId = habitNodeId;
        var list = await _notes.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(true);
        Notes.Clear();
        foreach (var n in list) Notes.Add(new NoteRowViewModel(n));
        SelectedNote = null;
        EditorMarkdown = string.Empty;
    }

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }
}
