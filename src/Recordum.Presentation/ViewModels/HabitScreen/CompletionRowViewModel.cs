using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.Presentation.ViewModels.HabitScreen;

public sealed partial class CompletionRowViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly Action<CompletionRowViewModel>? _onDeleted;
    private readonly string? _overrideNotes;
    private readonly Func<Guid, CancellationToken, Task>? _deleteOverride;
    private readonly IConfirmationDialog? _confirm;

    /// <summary>Il confine dell'abitudine, dalla lista che possiede la riga. Serve alla modifica a
    /// mano della data: l'utente scrive un orario di parete, non un istante UTC.</summary>
    private readonly DayBoundary _day;

    public Completion Completion { get; }
    public IReadOnlyList<CompletionValue> Values { get; }

    /// <summary>Pre-formatted "metric: value unit" / "metric: scale-label" lines for this completion's fields.</summary>
    public IReadOnlyList<string> FieldDisplays { get; }
    public bool HasFieldDisplays => FieldDisplays.Count > 0;

    /// <summary>I tag della voce di diario (stile di F2d-1); vuota fuori dai diari.</summary>
    public IReadOnlyList<TagChip> TagChips { get; }
    public bool HasTagChips => TagChips.Count > 0;

    /// <summary>False for journal rows: the diary text is edited in the Note column, not inline here.</summary>
    public bool CanInlineEdit { get; }

    public Guid Id => Completion.Id;
    public DateTime CompletedAt => Completion.CompletedAt;
    public bool IsRetroactive => Completion.IsRetroactive;
    public string? Notes => _overrideNotes ?? Completion.Notes;
    public string Display => ToWallTime(CompletedAt).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Il giorno della registrazione secondo il confine dell'abitudine, breve ("mer 28 ott").</summary>
    public string DayLabel => _day.DayOf(Completion.CompletedAtMs).ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>L'ora di parete nel formato breve della cultura.</summary>
    public string TimeLabel => ToWallTime(CompletedAt).ToString("t", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>La prima riga non vuota della nota, senza i segni Markdown di titolo, elenco, citazione ed enfasi.</summary>
    public string? Preview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Notes)) return null;
            foreach (var raw in Notes.Split('\n'))
            {
                var line = MarkdownLead().Replace(raw, string.Empty).Replace("**", string.Empty).Replace("__", string.Empty).Trim();
                if (line.Length > 0) return line;
            }
            return null;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*(#{1,6}|>|[-*+]|\d+\.)\s+")]
    private static partial System.Text.RegularExpressions.Regex MarkdownLead();

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
        bool canInlineEdit = true,
        DayBoundary? dayBoundary = null,
        IConfirmationDialog? confirm = null,
        IReadOnlyList<TagChip>? tagChips = null)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(completions);
        Completion = completion;
        _completions = completions;
        _onDeleted = onDeleted;
        Values = values ?? [];
        FieldDisplays = fieldDisplays ?? [];
        TagChips = tagChips ?? [];
        _overrideNotes = overrideNotes;
        _deleteOverride = deleteOverride;
        CanInlineEdit = canInlineEdit;
        _day = dayBoundary ?? DayBoundary.Utc;
        _confirm = confirm;
        EditedNotes = completion.Notes;
        EditedDate = ToWallTime(completion.CompletedAt);
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditedNotes = Notes;
        EditedDate = ToWallTime(CompletedAt);
        IsEditing = true;
    }

    /// <summary>L'istante salvato, letto come orologio di parete nel fuso dell'abitudine — la forma in
    /// cui un umano lo scriverebbe, e quella che <see cref="Display"/> mostra.</summary>
    private DateTime ToWallTime(DateTime utcTimestamp)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc), _day.TimeZone);

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveEditAsync(CancellationToken ct)
    {
        var newNotes = EditedNotes;
        // L'utente modifica un orario di parete: convertirlo dal fuso dell'abitudine, non dichiararlo
        // UTC. IsRetroactive NON cambia: spostare la data a mano non trasforma una registrazione live
        // in retroattiva, e cambiarlo qui altererebbe i conteggi di recovery Retroactive.
        var edited = new DateTimeOffset(EditedDate, _day.TimeZone.GetUtcOffset(EditedDate));
        var newDate = edited.UtcDateTime;
        var newMs = edited.ToUnixTimeMilliseconds();
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
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(CompletedAt));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(DayLabel));
        OnPropertyChanged(nameof(TimeLabel));
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken ct)
    {
        if (_confirm is not null
            && !await _confirm.ConfirmAsync(Strings.Abstinence_DeleteConfirm_Title, Strings.Records_DeleteConfirm_Message).ConfigureAwait(true))
            return;
        if (_deleteOverride is not null)
            await _deleteOverride(Id, ct).ConfigureAwait(true);
        else
            await _completions.DeleteAsync(Id, ct).ConfigureAwait(true);
        _onDeleted?.Invoke(this);
    }
}
