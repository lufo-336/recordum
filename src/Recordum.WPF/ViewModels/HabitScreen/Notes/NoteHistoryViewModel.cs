using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;

namespace Recordum.WPF.ViewModels.HabitScreen.Notes;

/// <summary>Inline version-history panel for one note: list of saved versions + read-only
/// preview + non-destructive restore. Raises <see cref="Restored"/> so the host can leave the panel.</summary>
public sealed partial class NoteHistoryViewModel(INoteService notes) : ObservableObject
{
    private Guid _noteId;

    public ObservableCollection<NoteVersionRowViewModel> Versions { get; } = [];

    [ObservableProperty] private NoteVersionRowViewModel? _selectedVersion;

    public string PreviewMarkdown => SelectedVersion?.BodyMd ?? string.Empty;

    public event EventHandler? Restored;

    public async Task LoadAsync(Guid noteId, CancellationToken ct = default)
    {
        _noteId = noteId;
        var versions = await notes.ListVersionsAsync(noteId, ct).ConfigureAwait(true);
        Versions.Clear();
        for (var i = 0; i < versions.Count; i++)
            Versions.Add(new NoteVersionRowViewModel(versions[i], isCurrent: i == 0));
        SelectedVersion = Versions.FirstOrDefault();
    }

    partial void OnSelectedVersionChanged(NoteVersionRowViewModel? value)
    {
        OnPropertyChanged(nameof(PreviewMarkdown));
        RestoreCommand.NotifyCanExecuteChanged();
    }

    private bool CanRestore() => SelectedVersion is not null;

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task Restore(CancellationToken ct)
    {
        if (SelectedVersion is not { } v) return;
        await notes.RestoreVersionAsync(_noteId, v.Id, ct).ConfigureAwait(true);
        Restored?.Invoke(this, EventArgs.Empty);
    }
}
